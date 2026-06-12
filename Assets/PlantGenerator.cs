using UnityEngine;

public class PlantGenerator : MonoBehaviour
{
    public float height = 5f; // Height of the plant
    public int numberOfBranches = 3; // Number of branches
    public GameObject branchPrefab; // Prefab for the branch

    void Start()
    {
        GeneratePlant();
    }

    void GeneratePlant()
    {
        // Create the main stem
        GameObject stem = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        stem.transform.localScale = new Vector3(0.2f, height / 2, 0.2f); // Scale the stem
        stem.transform.position = transform.position + new Vector3(0, height / 2, 0); // Position the stem

        // Generate branches
        for (int i = 0; i < numberOfBranches; i++)
        {
            CreateBranch(stem.transform);
        }
    }

    void CreateBranch(Transform parent)
    {
        // Create a branch
        GameObject branch = Instantiate(branchPrefab);
        float branchHeight = Random.Range(1f, height / 2); // Random height for the branch
        branch.transform.localScale = new Vector3(0.1f, branchHeight / 2, 0.1f); // Scale the branch
        branch.transform.SetParent(parent); // Set the parent to the stem

        // Position the branch
        float angle = Random.Range(0, 360); // Random angle for the branch
        float offsetX = Mathf.Cos(angle * Mathf.Deg2Rad) * 0.5f; // Calculate X offset
        float offsetZ = Mathf.Sin(angle * Mathf.Deg2Rad) * 0.5f; // Calculate Z offset
        branch.transform.position = parent.position + new Vector3(offsetX, height / 2 + branchHeight / 2, offsetZ);
        branch.transform.rotation = Quaternion.Euler(0, angle, 0); // Rotate the branch
    }
}
