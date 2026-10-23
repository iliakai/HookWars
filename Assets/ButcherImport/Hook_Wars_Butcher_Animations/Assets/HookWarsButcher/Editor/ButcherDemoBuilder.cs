#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HookWars.Butcher.Editor
{
    public static class ButcherDemoBuilder
    {
        const string Root="Assets/HookWarsButcher";
        [MenuItem("Hook Wars/Create Animated Butcher Demo")]
        public static void Build()
        {
            string modelPath=Root+"/Models/Butcher_Animated.fbx";
            ModelImporter importer=AssetImporter.GetAtPath(modelPath) as ModelImporter;
            if(importer==null)throw new InvalidOperationException("Copy Assets/HookWarsButcher into the project's Assets folder first.");
            importer.animationType=ModelImporterAnimationType.Generic;importer.avatarSetup=ModelImporterAvatarSetup.CreateFromThisModel;
            importer.importAnimation=true;importer.optimizeGameObjects=false;importer.animationCompression=ModelImporterAnimationCompression.Off;
            importer.SaveAndReimport();
            var clipSettings=importer.defaultClipAnimations;
            foreach(var c in clipSettings){c.loopTime=c.name.Contains("Roll_")||c.name.Contains("Idle");c.loopPose=false;c.lockRootHeightY=true;c.lockRootPositionXZ=true;c.lockRootRotation=true;}
            importer.clipAnimations=clipSettings;importer.SaveAndReimport();
            GameObject model=AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            var clips=AssetDatabase.LoadAllAssetsAtPath(modelPath).OfType<AnimationClip>().Where(c=>!c.name.StartsWith("__preview__")).ToArray();
            Func<string,AnimationClip> clip=n=>clips.FirstOrDefault(c=>c.name==n||c.name.EndsWith("|"+n)||c.name.EndsWith("_"+n))??throw new InvalidOperationException("Missing FBX animation: "+n);
            string generated=AssetDatabase.GenerateUniqueAssetPath(Root+"/Generated");AssetDatabase.CreateFolder(Root,Path.GetFileName(generated));
            var materials=new Dictionary<string,Material>();
            Shader shader=Shader.Find("Universal Render Pipeline/Lit");if(shader==null)shader=Shader.Find("HDRP/Lit");if(shader==null)shader=Shader.Find("Standard");
            foreach(string file in Directory.GetFiles(Root+"/Textures","*.png"))
            {
                string path=file.Replace('\\','/'),name=Path.GetFileNameWithoutExtension(file);Texture2D tex=AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                var mat=new Material(shader){name=name};if(mat.HasProperty("_BaseMap"))mat.SetTexture("_BaseMap",tex);if(mat.HasProperty("_MainTex"))mat.SetTexture("_MainTex",tex);if(mat.HasProperty("_BaseColorMap"))mat.SetTexture("_BaseColorMap",tex);
                if(mat.HasProperty("_Smoothness"))mat.SetFloat("_Smoothness",.18f);if(mat.HasProperty("_Glossiness"))mat.SetFloat("_Glossiness",.18f);
                if(mat.HasProperty("_Metallic"))mat.SetFloat("_Metallic",name.Contains("Steel")||name.Contains("Iron")?.25f:0);
                AssetDatabase.CreateAsset(mat,generated+"/"+name+".mat");materials[name]=mat;
            }
            GameObject temp=(GameObject)PrefabUtility.InstantiatePrefab(model);temp.name="ButcherVisual";
            foreach(Renderer renderer in temp.GetComponentsInChildren<Renderer>(true))
            {var a=renderer.sharedMaterials;for(int i=0;i<a.Length;i++)if(a[i]!=null&&materials.TryGetValue(a[i].name,out var mat))a[i]=mat;renderer.sharedMaterials=a;}
            Animator animator=temp.GetComponent<Animator>();if(animator==null)animator=temp.AddComponent<Animator>();
            animator.avatar=AssetDatabase.LoadAllAssetsAtPath(modelPath).OfType<Avatar>().FirstOrDefault();animator.applyRootMotion=false;animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
            var ctrl=AnimatorController.CreateAnimatorControllerAtPath(generated+"/Butcher.controller");
            ctrl.AddParameter("MoveX",AnimatorControllerParameterType.Float);ctrl.AddParameter("MoveY",AnimatorControllerParameterType.Float);ctrl.AddParameter("RollRate",AnimatorControllerParameterType.Float);
            var tree=new BlendTree{name="Eight directions",blendType=BlendTreeType.FreeformDirectional2D,blendParameter="MoveX",blendParameterY="MoveY",useAutomaticThresholds=false};AssetDatabase.AddObjectToAsset(tree,ctrl);
            tree.AddChild(clip("Idle"),Vector2.zero);
            string[] names={"N","NE","E","SE","S","SW","W","NW"};Vector2[] dirs={Vector2.up,new Vector2(1,1).normalized,Vector2.right,new Vector2(1,-1).normalized,Vector2.down,new Vector2(-1,-1).normalized,Vector2.left,new Vector2(-1,1).normalized};
            for(int i=0;i<8;i++)tree.AddChild(clip("Roll_"+names[i]),dirs[i]);
            var state=ctrl.layers[0].stateMachine.AddState("Roll8");state.motion=tree;state.speedParameter="RollRate";state.speedParameterActive=true;ctrl.layers[0].stateMachine.defaultState=state;
            AddLayer(ctrl,temp.transform,"Axe","AxePivot",clip("Idle"),clip("Axe_Attack"),"Axe_Attack",generated);
            AddLayer(ctrl,temp.transform,"Hook","ChainArm",clip("Idle"),clip("Hook_Cast"),"Hook_Cast",generated);
            animator.runtimeAnimatorController=ctrl;
            GameObject player=new GameObject("HookWars_Butcher");temp.transform.SetParent(player.transform,false);
            CharacterController motor=player.AddComponent<CharacterController>();motor.height=2.9f;motor.radius=.68f;motor.center=new Vector3(0,1.45f,0);motor.skinWidth=.04f;
            var driver=player.AddComponent<ButcherAnimationDriver>();driver.animator=animator;driver.bodyRoll=Find(temp.transform,"BodyRoll");driver.axeHit=Find(temp.transform,"AxeHit");
            var hook=player.AddComponent<PhysicalHookChain>();driver.hook=hook;hook.socket=Find(temp.transform,"HookSocket");
            hook.restingHook=temp.GetComponentsInChildren<Renderer>(true).FirstOrDefault(r=>r.name.StartsWith("04_Hook_Metal"));
            GameObject projectileAsset=AssetDatabase.LoadAssetAtPath<GameObject>(Root+"/Models/HookProjectile.fbx");
            GameObject projectile=(GameObject)PrefabUtility.InstantiatePrefab(projectileAsset);
            foreach(Renderer r in projectile.GetComponentsInChildren<Renderer>())r.sharedMaterial=materials["Cleaver_Steel"];
            hook.projectileVisual=PrefabUtility.SaveAsPrefabAsset(projectile,generated+"/HookProjectile.prefab");UnityEngine.Object.DestroyImmediate(projectile);
            var chainMat=new Material(shader){name="ChainLinks"};if(chainMat.HasProperty("_BaseColor"))chainMat.SetColor("_BaseColor",new Color(.16f,.18f,.20f));if(chainMat.HasProperty("_Color"))chainMat.color=new Color(.16f,.18f,.20f);if(chainMat.HasProperty("_Metallic"))chainMat.SetFloat("_Metallic",.7f);AssetDatabase.CreateAsset(chainMat,generated+"/ChainLinks.mat");hook.chainMaterial=chainMat;
            GameObject prefab=PrefabUtility.SaveAsPrefabAsset(player,generated+"/Butcher.prefab");UnityEngine.Object.DestroyImmediate(player);
            // Additive scene preserves any unsaved scene already open in the editor.
            Scene scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);SceneManager.SetActiveScene(scene);
            GameObject instance=(GameObject)PrefabUtility.InstantiatePrefab(prefab,scene);instance.transform.position=new Vector3(0,.08f,0);
            var input=instance.AddComponent<ButcherDemoInput>();input.driver=instance.GetComponent<ButcherAnimationDriver>();
            GameObject ground=GameObject.CreatePrimitive(PrimitiveType.Cube);ground.name="Ground";ground.transform.position=new Vector3(0,-.25f,0);ground.transform.localScale=new Vector3(30,.5f,30);
            for(int i=0;i<3;i++){GameObject block=GameObject.CreatePrimitive(PrimitiveType.Cube);block.name="Chain obstacle "+i;block.transform.position=new Vector3((i-1)*3,1.2f,5);block.transform.localScale=new Vector3(.8f,2.4f,.8f);}
            GameObject target=GameObject.CreatePrimitive(PrimitiveType.Cube);target.name="Pullable target";target.transform.position=new Vector3(-4,.5f,2);target.AddComponent<Rigidbody>().mass=2;
            GameObject cam=new GameObject("Camera");cam.tag="MainCamera";var camera=cam.AddComponent<Camera>();cam.AddComponent<AudioListener>();cam.transform.position=new Vector3(5,9,10);cam.transform.LookAt(new Vector3(0,1,1));camera.orthographic=true;camera.orthographicSize=8;input.viewCamera=camera;
            GameObject light=new GameObject("Sun");light.AddComponent<Light>().type=LightType.Directional;light.transform.rotation=Quaternion.Euler(45,-30,0);
            EditorSceneManager.SaveScene(scene,generated+"/ButcherDemo.unity");AssetDatabase.SaveAssets();Selection.activeGameObject=instance;
            Debug.Log("Butcher demo created at "+generated+". WASD/mouse controls. Physics settings are on PhysicalHookChain; no existing assets were overwritten.");
        }
        static Transform Find(Transform root,string name){return root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t=>t.name==name)??throw new InvalidOperationException("Missing bone "+name);}
        static void AddLayer(AnimatorController ctrl,Transform root,string label,string boneName,AnimationClip idle,AnimationClip action,string stateName,string directory)
        {
            var mask=new AvatarMask{name=label+"Mask"};var transforms=root.GetComponentsInChildren<Transform>(true);mask.transformCount=transforms.Length;
            for(int i=0;i<transforms.Length;i++)mask.SetTransformPath(i,AnimationUtility.CalculateTransformPath(transforms[i],root));
            string path=AnimationUtility.CalculateTransformPath(Find(root,boneName),root);
            for(int i=0;i<mask.transformCount;i++){string p=mask.GetTransformPath(i);mask.SetTransformActive(i,p==path||p.StartsWith(path+"/"));}
            for(int i=0;i<(int)AvatarMaskBodyPart.LastBodyPart;i++)mask.SetHumanoidBodyPartActive((AvatarMaskBodyPart)i,false);
            AssetDatabase.CreateAsset(mask,directory+"/"+label+".mask");
            ctrl.AddLayer(label);var layers=ctrl.layers;var layer=layers[layers.Length-1];layer.defaultWeight=1;layer.avatarMask=mask;ctrl.layers=layers;
            var rest=layer.stateMachine.AddState("Rest");rest.motion=idle;var attack=layer.stateMachine.AddState(stateName);attack.motion=action;layer.stateMachine.defaultState=rest;
            var transition=attack.AddTransition(rest);transition.hasExitTime=true;transition.exitTime=1;transition.duration=.03f;
        }
    }
}
#endif
