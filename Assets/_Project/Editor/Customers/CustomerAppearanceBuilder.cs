using System.Collections.Generic;
using System.Linq;
using Akila.FPSFramework;
using ShooterShop.Customers;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace ShooterShop.Editor.Customers
{
    public static class CustomerAppearanceBuilder
    {
        private const string CitizensPrefabs = "Assets/Citizens PRO/People Prefabs";
        private const string CitizensAnimations = "Assets/Citizens PRO/Animations";
        private const string AkilaPlayer = "Assets/ThirdParty/Akila/FPS Framework Pro/Prefabs/Characters/Player Network.prefab";
        private const string AnimationsFolder = "Assets/_Project/Art/Animations/Customers";
        private const string PistolIdlePath = AnimationsFolder + "/Pistol Idle.fbx";
        private const string RifleIdlePath = AnimationsFolder + "/Rifle Aiming Idle.fbx";
        private const string RifleFirePath = AnimationsFolder + "/Firing Rifle.fbx";
        private const string ControllerPath = AnimationsFolder + "/CustomerHumanoid.controller";
        private const string UpperBodyMaskPath = AnimationsFolder + "/CustomerUpperBody.mask";
        private const string AppearancesFolder = "Assets/_Project/Prefabs/Customers/Appearances";
        private const string CatalogPath = "Assets/_Project/Configs/CustomerAppearanceCatalog.asset";

        private static readonly string[] SourceFolders =
        {
            CitizensPrefabs + "/Male/Summer",
            CitizensPrefabs + "/Male/Winter",
            CitizensPrefabs + "/Female/Summer",
            CitizensPrefabs + "/Female/Winter"
        };

        private static readonly (string Controller, string Folder, string Suffix)[] Gaits =
        {
            ("man controller", "Man", ""),
            ("Girl with Heel controller", "Girl with Heel", "_f"),
            ("Girl no Heel controller", "Girl no Heel", "_f")
        };

        [MenuItem("Shooter Shop/Customers/Build Appearances")]
        public static void Build()
        {
            EnsureFolder(AnimationsFolder);
            EnsureFolder(AppearancesFolder);

            var sources = FindSourcePrefabs();
            ConvertModels(sources);
            var gaitClips = Gaits.ToDictionary(gait => gait.Controller, gait => ConvertGait(gait.Folder, gait.Suffix));
            var pistolIdle = ConvertStance(PistolIdlePath, true);
            var rifleIdle = ConvertStance(RifleIdlePath, true);
            var rifleFire = ConvertStance(RifleFirePath, false);
            var mask = BuildUpperBodyMask();
            var controller = BuildController(gaitClips[Gaits[0].Controller], pistolIdle, rifleIdle, rifleFire, mask);
            var overrides = Gaits.Skip(1).ToDictionary(gait => gait.Controller, gait => BuildOverride(controller, gait.Folder, gaitClips[Gaits[0].Controller], gaitClips[gait.Controller]));

            var appearances = new List<CustomerAppearance>();

            foreach (var source in sources)
            {
                var appearance = BuildAppearance(source, controller, overrides);

                if (appearance != null)
                    appearances.Add(appearance);
            }

            var catalog = LoadOrCreate<CustomerAppearanceCatalog>(CatalogPath);
            catalog.EditorSetAppearances(appearances);

            AssetDatabase.SaveAssets();
            Debug.Log($"[CustomerAppearanceBuilder] Built {appearances.Count} of {sources.Count} appearances");
        }

        private static List<GameObject> FindSourcePrefabs() =>
            AssetDatabase.FindAssets("t:Prefab", SourceFolders)
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<GameObject>)
                .Where(prefab => prefab != null && prefab.GetComponentInChildren<SkinnedMeshRenderer>(true) != null)
                .ToList();

        private static void ConvertModels(IEnumerable<GameObject> sources)
        {
            var models = sources
                .Select(source => AssetDatabase.GetAssetPath(source.GetComponentInChildren<SkinnedMeshRenderer>(true).sharedMesh))
                .Distinct();

            foreach (var model in models)
                MakeHumanoid(model, false);
        }

        private static (AnimationClip Idle, AnimationClip Walk, AnimationClip Run) ConvertGait(string folder, string suffix)
        {
            AnimationClip Clip(string name)
            {
                var path = $"{CitizensAnimations}/{folder}/{name}{suffix}.FBX";
                MakeHumanoid(path, true);
                return AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().FirstOrDefault(clip => !clip.name.StartsWith("__preview"));
            }

            return (Clip("idle1"), Clip("walk"), Clip("run"));
        }

        private static void MakeHumanoid(string path, bool isAnimation)
        {
            if (AssetImporter.GetAtPath(path) is not ModelImporter importer)
                return;

            var changed = importer.animationType != ModelImporterAnimationType.Human || importer.avatarSetup != ModelImporterAvatarSetup.CreateFromThisModel;
            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;

            if (isAnimation)
            {
                importer.importAnimation = true;
                var clips = importer.defaultClipAnimations;

                foreach (var clip in clips)
                    MakeLoopingInPlace(clip);

                importer.clipAnimations = clips;
                changed = true;
            }

            if (changed)
                importer.SaveAndReimport();
        }

        private static void MakeLoopingInPlace(ModelImporterClipAnimation clip)
        {
            clip.loopTime = true;
            clip.lockRootRotation = true;
            clip.lockRootHeightY = true;
            clip.lockRootPositionXZ = true;
            clip.keepOriginalOrientation = true;
            clip.keepOriginalPositionY = true;
            clip.keepOriginalPositionXZ = true;
        }

        private static AnimationClip ConvertStance(string path, bool loop)
        {
            if (AssetImporter.GetAtPath(path) is not ModelImporter importer)
                return null;

            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;

            var clips = importer.defaultClipAnimations;

            foreach (var clip in clips)
            {
                clip.name = System.IO.Path.GetFileNameWithoutExtension(path);
                MakeLoopingInPlace(clip);
                clip.loopTime = loop;
            }

            importer.clipAnimations = clips;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().FirstOrDefault(clip => !clip.name.StartsWith("__preview"));
        }

        private static AvatarMask BuildUpperBodyMask()
        {
            var mask = LoadOrCreate<AvatarMask>(UpperBodyMaskPath);

            for (var part = AvatarMaskBodyPart.Root; part < AvatarMaskBodyPart.LastBodyPart; part++)
                mask.SetHumanoidBodyPartActive(part, part is AvatarMaskBodyPart.Body or AvatarMaskBodyPart.Head
                    or AvatarMaskBodyPart.LeftArm or AvatarMaskBodyPart.RightArm
                    or AvatarMaskBodyPart.LeftFingers or AvatarMaskBodyPart.RightFingers);

            EditorUtility.SetDirty(mask);
            return mask;
        }

        private static AnimatorController BuildController((AnimationClip Idle, AnimationClip Walk, AnimationClip Run) gait, AnimationClip pistolIdle, AnimationClip rifleIdle, AnimationClip rifleFire, AvatarMask mask)
        {
            if (AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath) != null)
                AssetDatabase.DeleteAsset(ControllerPath);

            var controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            controller.AddParameter("MoveX", AnimatorControllerParameterType.Float);
            controller.AddParameter("MoveY", AnimatorControllerParameterType.Float);
            controller.AddParameter("IsGrounded", AnimatorControllerParameterType.Bool);

            var tree = new BlendTree
            {
                name = "Locomotion",
                blendType = BlendTreeType.FreeformDirectional2D,
                blendParameter = "MoveX",
                blendParameterY = "MoveY",
                hideFlags = HideFlags.HideInHierarchy
            };
            AssetDatabase.AddObjectToAsset(tree, controller);
            tree.AddChild(gait.Idle, Vector2.zero);
            tree.AddChild(gait.Walk, new Vector2(0f, 5f));
            tree.AddChild(gait.Run, new Vector2(0f, 10f));
            tree.AddChild(gait.Walk, new Vector2(5f, 0f));
            tree.AddChild(gait.Walk, new Vector2(-5f, 0f));
            tree.AddChild(gait.Walk, new Vector2(0f, -5f));

            var baseMachine = controller.layers[0].stateMachine;
            var locomotion = baseMachine.AddState("Locomotion");
            locomotion.motion = tree;
            baseMachine.defaultState = locomotion;

            controller.AddLayer(new AnimatorControllerLayer
            {
                name = "Weapon",
                defaultWeight = 0f,
                avatarMask = mask,
                blendingMode = AnimatorLayerBlendingMode.Override,
                stateMachine = new AnimatorStateMachine { name = "Weapon", hideFlags = HideFlags.HideInHierarchy }
            });

            var weaponMachine = controller.layers[1].stateMachine;
            AssetDatabase.AddObjectToAsset(weaponMachine, controller);

            var pistolState = weaponMachine.AddState("Pistol");
            pistolState.motion = pistolIdle;
            weaponMachine.defaultState = pistolState;

            var rifleState = weaponMachine.AddState("Rifle");
            rifleState.motion = rifleIdle;

            var fireState = weaponMachine.AddState("Rifle Fire");
            fireState.motion = rifleFire;
            var back = fireState.AddTransition(rifleState);
            back.hasExitTime = true;
            back.exitTime = 0.95f;
            back.duration = 0.15f;

            EditorUtility.SetDirty(controller);
            return controller;
        }

        private static AnimatorOverrideController BuildOverride(AnimatorController controller, string name,
            (AnimationClip Idle, AnimationClip Walk, AnimationClip Run) from, (AnimationClip Idle, AnimationClip Walk, AnimationClip Run) to)
        {
            var path = $"{AnimationsFolder}/CustomerHumanoid {name}.overrideController";
            var overrideController = LoadOrCreate<AnimatorOverrideController>(path);
            overrideController.runtimeAnimatorController = controller;
            overrideController[from.Idle] = to.Idle;
            overrideController[from.Walk] = to.Walk;
            overrideController[from.Run] = to.Run;
            EditorUtility.SetDirty(overrideController);
            return overrideController;
        }

        private static CustomerAppearance BuildAppearance(GameObject source, AnimatorController controller, Dictionary<string, AnimatorOverrideController> overrides)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(source);

            try
            {
                var animator = instance.GetComponentInChildren<Animator>(true);
                var mesh = instance.GetComponentInChildren<SkinnedMeshRenderer>(true).sharedMesh;
                var avatar = AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(mesh)).OfType<Avatar>().FirstOrDefault();

                if (animator == null || avatar == null || !avatar.isHuman)
                {
                    Debug.LogWarning($"[CustomerAppearanceBuilder] {source.name} is not humanoid, skipped", source);
                    return null;
                }

                var originalController = animator.runtimeAnimatorController != null ? animator.runtimeAnimatorController.name : string.Empty;
                animator.avatar = avatar;
                animator.runtimeAnimatorController = overrides.TryGetValue(originalController, out var gait) ? gait : controller;
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
                animator.Rebind();

                foreach (var child in instance.GetComponentsInChildren<Transform>(true))
                    child.gameObject.layer = 0;

                var ragdoll = instance.AddComponent<CustomerRagdoll>();
                var (bodies, hitboxes) = BuildRagdoll(animator);

                var serializedRagdoll = new SerializedObject(ragdoll);
                serializedRagdoll.FindProperty("_animator").objectReferenceValue = animator;
                serializedRagdoll.FindProperty("_sinkRoot").objectReferenceValue = instance.transform;
                Fill(serializedRagdoll.FindProperty("_bodies"), bodies);
                Fill(serializedRagdoll.FindProperty("_hitboxes"), hitboxes);
                serializedRagdoll.ApplyModifiedPropertiesWithoutUndo();

                var appearance = instance.AddComponent<CustomerAppearance>();
                appearance.EditorSetup(animator, ragdoll, animator.GetBoneTransform(HumanBodyBones.RightHand), instance.GetComponentsInChildren<SkinnedMeshRenderer>(true));

                var saved = PrefabUtility.SaveAsPrefabAsset(instance, $"{AppearancesFolder}/{source.name}.prefab");
                return saved.GetComponent<CustomerAppearance>();
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        private static (List<Rigidbody> Bodies, List<Collider> Hitboxes) BuildRagdoll(Animator animator)
        {
            var bloodTemplate = AssetDatabase.LoadAssetAtPath<GameObject>(AkilaPlayer).GetComponentInChildren<CustomDecal>(true);
            var bodies = new List<Rigidbody>();
            var hitboxes = new List<Collider>();
            var byBone = new Dictionary<HumanBodyBones, Rigidbody>();

            Transform Bone(HumanBodyBones bone) => animator.GetBoneTransform(bone);

            Rigidbody Add(HumanBodyBones bone, HumanBodyBones? parent, float mass, Collider collider, Vector2 twist, float swing)
            {
                var target = collider.transform;
                var body = target.gameObject.AddComponent<Rigidbody>();
                body.mass = mass;
                body.isKinematic = true;

                if (parent.HasValue && byBone.TryGetValue(parent.Value, out var parentBody))
                {
                    var joint = target.gameObject.AddComponent<CharacterJoint>();
                    joint.connectedBody = parentBody;
                    joint.enableProjection = true;
                    joint.lowTwistLimit = new SoftJointLimit { limit = twist.x };
                    joint.highTwistLimit = new SoftJointLimit { limit = twist.y };
                    joint.swing1Limit = new SoftJointLimit { limit = swing };
                    joint.swing2Limit = new SoftJointLimit { limit = swing };
                }

                if (bloodTemplate != null)
                    EditorUtility.CopySerialized(bloodTemplate, target.gameObject.AddComponent<CustomDecal>());

                byBone[bone] = body;
                bodies.Add(body);
                hitboxes.Add(collider);
                return body;
            }

            var hips = Bone(HumanBodyBones.Hips);
            var chest = Bone(HumanBodyBones.Chest) ?? Bone(HumanBodyBones.Spine);
            var head = Bone(HumanBodyBones.Head);
            var leftUpperLeg = Bone(HumanBodyBones.LeftUpperLeg);
            var rightUpperLeg = Bone(HumanBodyBones.RightUpperLeg);
            var legSpan = Vector3.Distance(leftUpperLeg.position, rightUpperLeg.position);

            Add(HumanBodyBones.Hips, null, 12f, WorldBox(hips, hips.position + (chest.position - hips.position) * 0.35f, new Vector3(legSpan * 1.8f, Vector3.Distance(hips.position, chest.position) * 0.9f, legSpan * 1.1f)), Vector2.zero, 0f);
            Add(HumanBodyBones.Chest, HumanBodyBones.Hips, 12f, WorldBox(chest, chest.position + (head.position - chest.position) * 0.4f, new Vector3(legSpan * 2.1f, Vector3.Distance(chest.position, head.position) * 0.9f, legSpan * 1.2f)), new Vector2(-20f, 20f), 15f);
            Add(HumanBodyBones.Head, HumanBodyBones.Chest, 5f, WorldSphere(head, head.position + Vector3.up * legSpan * 0.55f, legSpan * 0.6f), new Vector2(-40f, 25f), 25f);

            foreach (var (upper, lower, end, isArm) in new[]
            {
                (HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand, true),
                (HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand, true),
                (HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot, false),
                (HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot, false)
            })
            {
                var parent = isArm ? HumanBodyBones.Chest : HumanBodyBones.Hips;
                var radius = legSpan * (isArm ? 0.35f : 0.45f);
                Add(upper, parent, isArm ? 2.5f : 7f, WorldCapsule(Bone(upper), Bone(lower).position, radius), new Vector2(-30f, 30f), isArm ? 60f : 40f);
                Add(lower, upper, isArm ? 2f : 5f, WorldCapsule(Bone(lower), Bone(end).position, radius * 0.8f), new Vector2(-90f, 0f), 10f);
            }

            return (bodies, hitboxes);
        }

        private static Collider WorldBox(Transform bone, Vector3 worldCenter, Vector3 worldSize)
        {
            var box = bone.gameObject.AddComponent<BoxCollider>();
            box.center = bone.InverseTransformPoint(worldCenter);
            box.size = Abs(LocalSize(bone, worldSize));
            return box;
        }

        private static Collider WorldSphere(Transform bone, Vector3 worldCenter, float worldRadius)
        {
            var sphere = bone.gameObject.AddComponent<SphereCollider>();
            sphere.center = bone.InverseTransformPoint(worldCenter);
            sphere.radius = worldRadius / MaxScale(bone);
            return sphere;
        }

        private static Collider WorldCapsule(Transform bone, Vector3 worldEnd, float worldRadius)
        {
            var capsule = bone.gameObject.AddComponent<CapsuleCollider>();
            var localEnd = bone.InverseTransformPoint(worldEnd);
            var axis = LongestAxis(localEnd);
            capsule.direction = axis;
            capsule.center = localEnd * 0.5f;
            capsule.radius = worldRadius / MaxScale(bone);
            capsule.height = Mathf.Abs(localEnd[axis]) + capsule.radius * 2f;
            return capsule;
        }

        private static Vector3 LocalSize(Transform bone, Vector3 worldSize)
        {
            var size = Vector3.zero;
            var axes = new[] { Vector3.right, Vector3.up, Vector3.forward };

            for (var i = 0; i < 3; i++)
            {
                var localAxis = bone.InverseTransformDirection(axes[i]);
                var axis = LongestAxis(localAxis);
                size[axis] = worldSize[i] / Mathf.Abs(bone.lossyScale[axis]);
            }

            return size;
        }

        private static int LongestAxis(Vector3 vector)
        {
            var abs = Abs(vector);
            return abs.x >= abs.y && abs.x >= abs.z ? 0 : abs.y >= abs.z ? 1 : 2;
        }

        private static Vector3 Abs(Vector3 vector) => new(Mathf.Abs(vector.x), Mathf.Abs(vector.y), Mathf.Abs(vector.z));

        private static float MaxScale(Transform bone)
        {
            var scale = Abs(bone.lossyScale);
            return Mathf.Max(scale.x, Mathf.Max(scale.y, scale.z));
        }

        private static void Fill<T>(SerializedProperty list, IReadOnlyList<T> items) where T : Object
        {
            list.ClearArray();

            for (var i = 0; i < items.Count; i++)
            {
                list.InsertArrayElementAtIndex(i);
                list.GetArrayElementAtIndex(i).objectReferenceValue = items[i];
            }
        }

        private static T LoadOrCreate<T>(string path) where T : Object, new()
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);

            if (asset != null)
                return asset;

            asset = typeof(ScriptableObject).IsAssignableFrom(typeof(T)) ? (T)(Object)ScriptableObject.CreateInstance(typeof(T)) : new T();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;

            var parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
        }
    }
}
