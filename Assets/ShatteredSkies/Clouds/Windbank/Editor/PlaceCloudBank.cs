using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace ShatteredSkies.Clouds.Editor
{
    internal static class PlaceCloudBank
    {
        private const string Folder = "Assets/ShatteredSkies/Clouds/Windbank/";
        private const string PrefabPath = Folder + "Windbank.prefab";

        [MenuItem("Tools/Shattered Skies/Place Cloud Bank")]
        private static void Place()
        {
            if (Application.isPlaying)
            {
                Debug.LogWarning("Exit Play Mode to place a persistent cloud bank.");
                return;
            }
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (!prefab) prefab = Build();
            if (!prefab) return;
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            Undo.RegisterCreatedObjectUndo(instance, "Place Cloud Bank");
            instance.transform.position = new Vector3(90, 0, 35);
            Selection.activeGameObject = instance;
            EditorSceneManager.MarkSceneDirty(instance.scene);
            SceneView.lastActiveSceneView?.FrameSelected();
        }

        [MenuItem("Tools/Shattered Skies/Place Cloud Field")]
        private static void PlaceField()
        {
            if (Application.isPlaying) return;
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            var existing = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<CloudField>(true)).FirstOrDefault();
            if (existing)
            {
                Selection.activeGameObject = existing.gameObject;
                return;
            }
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (!prefab) prefab = Build();
            if (!prefab) return;
            var root = new GameObject("Island Cloud Field");
            Undo.RegisterCreatedObjectUndo(root, "Place Cloud Field");
            root.AddComponent<CloudField>().bankPrefab = prefab;
            Selection.activeGameObject = root;
            EditorSceneManager.MarkSceneDirty(root.scene);
        }

        private static Material MaterialFor(string name)
        {
            string path = Folder + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material) return material;
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(Folder + name + ".shader");
            if (!shader) throw new FileNotFoundException("Missing cloud shader", name);
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static GameObject Build()
        {
            var mesh = AssetDatabase.LoadAllAssetsAtPath(Folder + "Windbank.obj").OfType<Mesh>().FirstOrDefault();
            if (!mesh) throw new FileNotFoundException("Windbank.obj has not finished importing.");
            var bank = new GameObject("Windbank");
            try
            {
                var body = new GameObject("Drifting bank");
                body.transform.SetParent(bank.transform, false);
                body.AddComponent<CloudDrift>();
                var model = new GameObject("Cloud body");
                model.transform.SetParent(body.transform, false);
                model.transform.localScale = Vector3.one * (36f / mesh.bounds.size.x);
                model.AddComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = model.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = MaterialFor("CloudBank");
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                AddWisps(body.transform);
                return PrefabUtility.SaveAsPrefabAsset(bank, PrefabPath);
            }
            finally { Object.DestroyImmediate(bank); }
        }

        private static void AddWisps(Transform parent)
        {
            var tail = new GameObject("Trailing wisps");
            tail.transform.SetParent(parent, false);
            tail.transform.localPosition = new Vector3(-15, 0, 0);
            var particles = tail.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = particles.main;
            main.loop = true;
            main.prewarm = true;
            main.duration = 14;
            main.startLifetime = 13;
            main.maxParticles = 3;
            main.startSpeed = 0;
            main.startSize3D = true;
            main.startSizeX = 11;
            main.startSizeY = 2.4f;
            main.startSizeZ = 1;
            main.startColor = new Color(.9f,.95f,.93f,.45f);
            var emission = particles.emission;
            emission.rateOverTime = .22f;
            var shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(2, 1, 3);
            var velocity = particles.velocityOverLifetime;
            velocity.enabled = true;
            velocity.x = -.65f;
            var fade = particles.colorOverLifetime;
            fade.enabled = true;
            var alpha = new Gradient();
            alpha.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                new[] { new GradientAlphaKey(0,0), new GradientAlphaKey(1,.25f), new GradientAlphaKey(.8f,.6f), new GradientAlphaKey(0,1) });
            fade.color = alpha;
            var renderer = particles.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sharedMaterial = MaterialFor("CloudWisp");
            renderer.shadowCastingMode = ShadowCastingMode.Off;
        }
    }
}

