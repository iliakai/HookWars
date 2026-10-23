using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
namespace HookWars.Butcher
{
    // Optional demo only. Existing game controllers should call Attack / CastHook / Retract.
    [RequireComponent(typeof(CharacterController))]
    public sealed class ButcherDemoInput : MonoBehaviour
    {
        public Camera viewCamera; public ButcherAnimationDriver driver; public float speed=3;
        CharacterController motor;float fall;
        void Awake(){motor=GetComponent<CharacterController>();}
        void Update()
        {
            Vector2 input=Vector2.zero,mouse=Vector2.zero;bool attack=false,cast=false,retract=false;
#if ENABLE_INPUT_SYSTEM
            var k=Keyboard.current;var m=Mouse.current;
            if(k!=null){input.x=(k.dKey.isPressed||k.rightArrowKey.isPressed?1:0)-(k.aKey.isPressed||k.leftArrowKey.isPressed?1:0);input.y=(k.wKey.isPressed||k.upArrowKey.isPressed?1:0)-(k.sKey.isPressed||k.downArrowKey.isPressed?1:0);retract=k.rKey.wasPressedThisFrame;}
            if(m!=null){mouse=m.position.ReadValue();attack=m.leftButton.wasPressedThisFrame;cast=m.rightButton.wasPressedThisFrame;}
#elif ENABLE_LEGACY_INPUT_MANAGER
            input=new Vector2(Input.GetAxisRaw("Horizontal"),Input.GetAxisRaw("Vertical"));mouse=Input.mousePosition;
            attack=Input.GetMouseButtonDown(0);cast=Input.GetMouseButtonDown(1);retract=Input.GetKeyDown(KeyCode.R);
#endif
            if(viewCamera==null)viewCamera=Camera.main;if(viewCamera==null)return;
            Vector3 f=viewCamera.transform.forward;f.y=0;f.Normalize();Vector3 r=viewCamera.transform.right;r.y=0;r.Normalize();
            Vector3 move=Vector3.ClampMagnitude(f*input.y+r*input.x,1)*speed;
            if(motor.isGrounded && fall<0)fall=-2;fall+=Physics.gravity.y*Time.deltaTime;
            motor.Move((move+Vector3.up*fall)*Time.deltaTime);
            Ray ray=viewCamera.ScreenPointToRay(mouse);Plane plane=new Plane(Vector3.up,transform.position);
            Vector3 aim=transform.forward;
            if(plane.Raycast(ray,out float distance)){aim=ray.GetPoint(distance)-transform.position;aim.y=0;if(aim.sqrMagnitude>.01f)transform.rotation=Quaternion.LookRotation(aim);}
            if(attack)driver.Attack();if(cast)driver.CastHook(aim);if(retract && driver.hook!=null)driver.hook.Retract();
        }
        void OnGUI(){GUI.Label(new Rect(15,15,800,30),"WASD / arrows: move | Mouse: aim | LMB: axe | RMB: hook | R: reel in");}
    }
}
