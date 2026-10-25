using UnityEngine;

[DefaultExecutionOrder(1200)]
public sealed class BarracksVisual25D : MonoBehaviour
{
    [SerializeField] private Transform leftHinge,rightHinge;
    private Quaternion leftClosed,rightClosed;
    private Barracks source;
    private float opening;
    private float leftSign,rightSign;
    public void Bind(Barracks barracks)
    {
        source=barracks;leftClosed=leftHinge.localRotation;rightClosed=rightHinge.localRotation;
        leftSign=OpeningSign(leftHinge);rightSign=OpeningSign(rightHinge);
    }
    private float OpeningSign(Transform hinge)
    {
        Vector3 offset=hinge.GetComponentInChildren<Renderer>().bounds.center-hinge.position;
        return Vector3.Dot(Quaternion.AngleAxis(100,Vector3.up)*offset,transform.forward)>=Vector3.Dot(Quaternion.AngleAxis(-100,Vector3.up)*offset,transform.forward)?1:-1;
    }
    private void LateUpdate()
    {
        if(source==null)return;
        opening=Mathf.MoveTowards(opening,source.DoorOpening,Time.deltaTime*2);
        var leftAxis=leftHinge.parent.InverseTransformDirection(Vector3.up);
        var rightAxis=rightHinge.parent.InverseTransformDirection(Vector3.up);
        leftHinge.localRotation=Quaternion.AngleAxis(leftSign*100*opening,leftAxis)*leftClosed;
        rightHinge.localRotation=Quaternion.AngleAxis(rightSign*100*opening,rightAxis)*rightClosed;
    }
}
