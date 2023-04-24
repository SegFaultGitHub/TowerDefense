//
//  Outline.cs
//  QuickOutline
//
//  Created by Chris Nolet on 3/30/18.
//  Copyright © 2018 Chris Nolet. All rights reserved.
//

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

[DisallowMultipleComponent]
public class Outline : MonoBehaviour {

    public enum Mode {
        OutlineAll,
        OutlineVisible,
        OutlineHidden,
        OutlineAndSilhouette,
        SilhouetteOnly
    }
    private static readonly HashSet<Mesh> registeredMeshes = new();

    [SerializeField]
    private Mode outlineMode;

    [SerializeField]
    private Color outlineColor = Color.white;

    [SerializeField, Range(0f, 10f)]
    private float outlineWidth = 2f;

    [Header("Optional"), SerializeField,
     Tooltip("Precompute enabled: Per-vertex calculations are performed in the editor and serialized with the object. "
             + "Precompute disabled: Per-vertex calculations are performed at runtime in Awake(). This may cause a pause for large meshes.")]
    private bool precomputeOutline;

    [SerializeField, HideInInspector]
    private List<Mesh> bakeKeys = new();

    [SerializeField, HideInInspector]
    private List<ListVector3> bakeValues = new();

    private bool needsUpdate;
    private Material outlineFillMaterial;
    private Material outlineMaskMaterial;

    private Renderer[] renderers;

    public Mode OutlineMode {
        get =>
            this.outlineMode;
        set {
            this.outlineMode = value;
            this.needsUpdate = true;
        }
    }

    public Color OutlineColor {
        get =>
            this.outlineColor;
        set {
            this.outlineColor = value;
            this.needsUpdate = true;
        }
    }

    public float OutlineWidth {
        get =>
            this.outlineWidth;
        set {
            this.outlineWidth = value;
            this.needsUpdate = true;
        }
    }

    private void Awake() {

        // Cache renderers
        this.renderers = this.GetComponentsInChildren<Renderer>();

        // Instantiate outline materials
        this.outlineMaskMaterial = Instantiate(Resources.Load<Material>(@"Materials/OutlineMask"));
        this.outlineFillMaterial = Instantiate(Resources.Load<Material>(@"Materials/OutlineFill"));

        this.outlineMaskMaterial.name = "OutlineMask (Instance)";
        this.outlineFillMaterial.name = "OutlineFill (Instance)";

        // Retrieve or generate smooth normals
        this.LoadSmoothNormals();

        // Apply material properties immediately
        this.needsUpdate = true;
    }

    private void Update() {
        if (this.needsUpdate) {
            this.needsUpdate = false;

            this.UpdateMaterialProperties();
        }
    }

    private void OnEnable() {
        foreach (Renderer renderer in this.renderers) {

            // Append outline shaders
            List<Material> materials = renderer.sharedMaterials.ToList();

            materials.Add(this.outlineMaskMaterial);
            materials.Add(this.outlineFillMaterial);

            renderer.materials = materials.ToArray();
        }
    }

    private void OnDisable() {
        foreach (Renderer renderer in this.renderers) {

            // Remove outline shaders
            List<Material> materials = renderer.sharedMaterials.ToList();

            materials.Remove(this.outlineMaskMaterial);
            materials.Remove(this.outlineFillMaterial);

            renderer.materials = materials.ToArray();
        }
    }

    private void OnDestroy() {

        // Destroy material instances
        Destroy(this.outlineMaskMaterial);
        Destroy(this.outlineFillMaterial);
    }

    private void OnValidate() {

        // Update material properties
        this.needsUpdate = true;

        // Clear cache when baking is disabled or corrupted
        if (!this.precomputeOutline && this.bakeKeys.Count != 0 || this.bakeKeys.Count != this.bakeValues.Count) {
            this.bakeKeys.Clear();
            this.bakeValues.Clear();
        }

        // Generate smooth normals when baking is enabled
        if (this.precomputeOutline && this.bakeKeys.Count == 0) {
            this.Bake();
        }
    }

    private void Bake() {

        // Generate smooth normals for each mesh
        HashSet<Mesh> bakedMeshes = new HashSet<Mesh>();

        foreach (MeshFilter meshFilter in this.GetComponentsInChildren<MeshFilter>()) {

            // Skip duplicates
            if (!bakedMeshes.Add(meshFilter.sharedMesh)) {
                continue;
            }

            // Serialize smooth normals
            List<Vector3> smoothNormals = this.SmoothNormals(meshFilter.sharedMesh);

            this.bakeKeys.Add(meshFilter.sharedMesh);
            this.bakeValues.Add(new ListVector3 { data = smoothNormals });
        }
    }

    private void LoadSmoothNormals() {

        // Retrieve or generate smooth normals
        foreach (MeshFilter meshFilter in this.GetComponentsInChildren<MeshFilter>()) {

            // Skip if smooth normals have already been adopted
            if (!registeredMeshes.Add(meshFilter.sharedMesh)) {
                continue;
            }

            // Retrieve or generate smooth normals
            int index = this.bakeKeys.IndexOf(meshFilter.sharedMesh);
            List<Vector3> smoothNormals = index >= 0 ? this.bakeValues[index].data : this.SmoothNormals(meshFilter.sharedMesh);

            // Store smooth normals in UV3
            meshFilter.sharedMesh.SetUVs(3, smoothNormals);

            // Combine submeshes
            Renderer renderer = meshFilter.GetComponent<Renderer>();

            if (renderer != null) {
                this.CombineSubmeshes(meshFilter.sharedMesh, renderer.sharedMaterials);
            }
        }

        // Clear UV3 on skinned mesh renderers
        foreach (SkinnedMeshRenderer skinnedMeshRenderer in this.GetComponentsInChildren<SkinnedMeshRenderer>()) {

            // Skip if UV3 has already been reset
            if (!registeredMeshes.Add(skinnedMeshRenderer.sharedMesh)) {
                continue;
            }

            // Clear UV3
            skinnedMeshRenderer.sharedMesh.uv4 = new Vector2[skinnedMeshRenderer.sharedMesh.vertexCount];

            // Combine submeshes
            this.CombineSubmeshes(skinnedMeshRenderer.sharedMesh, skinnedMeshRenderer.sharedMaterials);
        }
    }

    private List<Vector3> SmoothNormals(Mesh mesh) {

        // Group vertices by location
        IEnumerable<IGrouping<Vector3, KeyValuePair<Vector3, int>>> groups =
            mesh.vertices.Select((vertex, index) => new KeyValuePair<Vector3, int>(vertex, index)).GroupBy(pair => pair.Key);

        // Copy normals to a new list
        List<Vector3> smoothNormals = new List<Vector3>(mesh.normals);

        // Average normals for grouped vertices
        foreach (IGrouping<Vector3, KeyValuePair<Vector3, int>> group in groups) {

            // Skip single vertices
            if (group.Count() == 1) {
                continue;
            }

            // Calculate the average normal
            Vector3 smoothNormal = Vector3.zero;

            foreach (KeyValuePair<Vector3, int> pair in group) {
                smoothNormal += smoothNormals[pair.Value];
            }

            smoothNormal.Normalize();

            // Assign smooth normal to each vertex
            foreach (KeyValuePair<Vector3, int> pair in group) {
                smoothNormals[pair.Value] = smoothNormal;
            }
        }

        return smoothNormals;
    }

    private void CombineSubmeshes(Mesh mesh, Material[] materials) {

        // Skip meshes with a single submesh
        if (mesh.subMeshCount == 1) {
            return;
        }

        // Skip if submesh count exceeds material count
        if (mesh.subMeshCount > materials.Length) {
            return;
        }

        // Append combined submesh
        mesh.subMeshCount++;
        mesh.SetTriangles(mesh.triangles, mesh.subMeshCount - 1);
    }

    private void UpdateMaterialProperties() {

        // Apply properties according to mode
        this.outlineFillMaterial.SetColor("_OutlineColor", this.outlineColor);

        switch (this.outlineMode) {
            case Mode.OutlineAll:
                this.outlineMaskMaterial.SetFloat("_ZTest", (float)CompareFunction.Always);
                this.outlineFillMaterial.SetFloat("_ZTest", (float)CompareFunction.Always);
                this.outlineFillMaterial.SetFloat("_OutlineWidth", this.outlineWidth);
                break;

            case Mode.OutlineVisible:
                this.outlineMaskMaterial.SetFloat("_ZTest", (float)CompareFunction.Always);
                this.outlineFillMaterial.SetFloat("_ZTest", (float)CompareFunction.LessEqual);
                this.outlineFillMaterial.SetFloat("_OutlineWidth", this.outlineWidth);
                break;

            case Mode.OutlineHidden:
                this.outlineMaskMaterial.SetFloat("_ZTest", (float)CompareFunction.Always);
                this.outlineFillMaterial.SetFloat("_ZTest", (float)CompareFunction.Greater);
                this.outlineFillMaterial.SetFloat("_OutlineWidth", this.outlineWidth);
                break;

            case Mode.OutlineAndSilhouette:
                this.outlineMaskMaterial.SetFloat("_ZTest", (float)CompareFunction.LessEqual);
                this.outlineFillMaterial.SetFloat("_ZTest", (float)CompareFunction.Always);
                this.outlineFillMaterial.SetFloat("_OutlineWidth", this.outlineWidth);
                break;

            case Mode.SilhouetteOnly:
                this.outlineMaskMaterial.SetFloat("_ZTest", (float)CompareFunction.LessEqual);
                this.outlineFillMaterial.SetFloat("_ZTest", (float)CompareFunction.Greater);
                this.outlineFillMaterial.SetFloat("_OutlineWidth", 0f);
                break;
        }
    }

    [Serializable]
    private class ListVector3 {
        public List<Vector3> data;
    }
}
