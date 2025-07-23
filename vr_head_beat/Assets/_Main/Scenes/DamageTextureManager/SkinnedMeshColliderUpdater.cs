using UnityEngine;

[RequireComponent(typeof(SkinnedMeshRenderer), typeof(MeshCollider))]
public class SkinnedMeshColliderUpdater : MonoBehaviour
{
    private SkinnedMeshRenderer skinnedRenderer;
    private MeshCollider meshCollider;
    private Mesh bakedMesh;

    void Awake()
    {
        skinnedRenderer = GetComponent<SkinnedMeshRenderer>();
        meshCollider = GetComponent<MeshCollider>();
        meshCollider.convex = false;
        bakedMesh = new Mesh();
    }

    void LateUpdate()
    {
        skinnedRenderer.BakeMesh(bakedMesh);
        meshCollider.sharedMesh = null; // Обязательно обнулить, чтобы обновилось
        meshCollider.sharedMesh = bakedMesh;
    }
}