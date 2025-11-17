using Unity.Cinemachine;
using UnityEngine;

public class DollyOffset : MonoBehaviour
{
    public CinemachineSplineCart cart;
    public CinemachineSplineCart targetCart;
    public float offset;

    // Update is called once per frame
    void LateUpdate()
    {
        if (cart == null || targetCart == null) return;

        cart.SplinePosition = targetCart.SplinePosition + offset;
    }
}