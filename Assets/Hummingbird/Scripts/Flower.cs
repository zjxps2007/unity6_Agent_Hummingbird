using UnityEngine;

public class Flower : MonoBehaviour
{
    [Tooltip("The color when the flower is full")]
    public Color fullFlowerColor = new Color(1f, 0f, .3f);
    
    [Tooltip("The color when the flower is empty")]
    public Color emptyFlowerColor = new Color(.5f, 0f, 1f);
    
    [HideInInspector]
    public Collider nectarCollider;
    
    private Material flowerMaterial;
    
    private Collider flowerCollider;

    public Vector3 FlowerUpVector
    {
        get
        {
            return nectarCollider.transform.up;
        }
    }

    public Vector3 FlowerCenterPosition
    {
        get
        {
            return nectarCollider.transform.position;
        }
    }

    public float NectarAmount { get; private set; }

    public bool HasNectar
    {
        get
        {
            return NectarAmount > 0f;
        }
    }

    public float Feed(float amount)
    {
        float nectarTaken = Mathf.Clamp(amount, 0f, NectarAmount);
        
        NectarAmount -= amount;

        if (NectarAmount <= 0f)
        {
            NectarAmount = 0f;
            
            flowerCollider.gameObject.SetActive(false);
            
            flowerMaterial.SetColor("_BaseColor", emptyFlowerColor);
        }
        
        return nectarTaken;
    }
    
    public void ResetFlower()
    {
        NectarAmount = 1f;
        
        flowerCollider.gameObject.SetActive(true);
        nectarCollider.gameObject.SetActive(true);
        
        flowerMaterial.SetColor("_BaseColor", fullFlowerColor);
    }

    private void Awake()
    {
        MeshRenderer meshRenderer = GetComponent<MeshRenderer>();
        flowerMaterial = meshRenderer.material;
        
        flowerCollider = transform.Find("FlowerCollider").GetComponent<Collider>();
        nectarCollider = transform.Find("FlowerNectarCollider").GetComponent<Collider>();
    }
}
