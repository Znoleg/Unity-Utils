using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using share.controller.GUI.Anims;
using Spine.Unity;
using UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Editor
{
    public class GUIOptimizationTool : EditorWindow
    {
        private GameObject _objectToOptimize;
        private bool _optimizeCanvasGroups;
        private Material _uiMaterial;
        private bool _changeOnlyDefaultMaterial = true;
        private bool _needOptimizeInFolder;
        
        private const string DefaultUIMaterialName = "Default UI Material";
        private const float SmallSeparatorWidth = 5f;
        private const float WideSeparatorWidth = 15f;
        private static readonly Type[] INTERACTABLE_TYPES = new[] { typeof(Button), typeof(AutoClickButton), 
            typeof(Toggle), typeof(PointEventsReceiver), typeof(IPointerClickHandler), 
            typeof(IPointerEnterHandler), typeof(IPointerExitHandler), typeof(IPointerDownHandler), 
            typeof(IPointerUpHandler), typeof(IDragHandler), typeof(IScrollHandler), typeof(Selectable)};

        private string _optimizeFolderPath;
        private string _findShaderName;
        private string _findMaterialName;

        public GUIOptimizationTool(GameObject objectToOptimize, bool optimizeCanvasGroups, Material uiMaterial, bool changeOnlyDefaultMaterial, bool needOptimizeInFolder, string optimizeFolderPath, string findShaderName, string findMaterialName)
        {
            _objectToOptimize = objectToOptimize;
            _optimizeCanvasGroups = optimizeCanvasGroups;
            _uiMaterial = uiMaterial;
            _changeOnlyDefaultMaterial = changeOnlyDefaultMaterial;
            _needOptimizeInFolder = needOptimizeInFolder;
            _optimizeFolderPath = optimizeFolderPath;
            _findShaderName = findShaderName;
            _findMaterialName = findMaterialName;
        }

        private bool HasCanvasGroups => _objectToOptimize.TryGetComponent(out CanvasGroup _) ||
                                        _objectToOptimize.GetComponentInChildren<CanvasGroup>(includeInactive: true) != null;
        
        [MenuItem("Resort/GUIOptimizationTool")]
        public static void ShowWindow()
        {
            GetWindow<GUIOptimizationTool>();
        }

        private void OnGUI()
        {
            float originalGUIWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = 220f;
            DrawInspectorFields();
            EditorGUIUtility.labelWidth = originalGUIWidth;

            if (GUILayout.Button("Выключить Raycast Targets"))
            {
                TurnOffRaycasts();
            }

            if (GUILayout.Button("Расставить UIMaterial"))
            {
                SetupUIMaterial();
            }
            
            EditorGUILayout.Space(SmallSeparatorWidth);

            if (GUILayout.Button("Включить случайно выключенные Raycast Targets"))
            {
                TurnOnRaycasts();
            }

            EditorGUILayout.Space(WideSeparatorWidth);
            
            _findShaderName = EditorGUILayout.TextField("Имя шейдера для поиска на используемых объектах: ", _findShaderName);
            if (GUILayout.Button("Найти шейдер"))
            {
                FindObjectsShaderUse();
            }

            _findMaterialName = EditorGUILayout.TextField("Имя материала для поиска на используемых объектах: ",_findMaterialName);
            if (GUILayout.Button("Найти материал"))
            {
                
            }
        }

        private void DrawInspectorFields()
        {
            _needOptimizeInFolder =  EditorGUILayout.Toggle("Оптимизировать всё в папке", _needOptimizeInFolder);
            if (_needOptimizeInFolder)
            {
                EditorGUILayout.LabelField("Путь к папке: ");
                _optimizeFolderPath = EditorGUILayout.TextArea(_optimizeFolderPath);
                EditorGUILayout.HelpBox("Пишем путь к конечной папке с префабами, оптимизируем, нажимаем File->Save в проекте", MessageType.Info);
            }
            else
            {
                _objectToOptimize = (GameObject)EditorGUILayout.ObjectField("GameObject для оптимизации",
                    _objectToOptimize, typeof(GameObject), true);

                EditorGUILayout.HelpBox("Открываем нужный объект, перетягиваем его из Hierarchy в это поле (не из Project!)", MessageType.Info);
            }

            EditorGUILayout.Space(SmallSeparatorWidth);

            if (_objectToOptimize != null)
            {
                if (HasCanvasGroups)
                {
                    _optimizeCanvasGroups = EditorGUILayout.Toggle("Выключать Raycast у Canvas Group", _optimizeCanvasGroups);
                    EditorGUILayout.HelpBox("Не включаем галочку, если в Canvas Group попадают кнопки в рантайме (например, через Instantiate)", MessageType.Warning);
                }
                else
                {
                    EditorGUILayout.HelpBox("У объекта нет Canvas Group", MessageType.Info);
                }
            }

            EditorGUILayout.Space(WideSeparatorWidth);

            _uiMaterial = (Material)EditorGUILayout.ObjectField("UIMaterial",
                _uiMaterial, typeof(Material), false);

            _changeOnlyDefaultMaterial =
                EditorGUILayout.Toggle($"Менять только {DefaultUIMaterialName}", _changeOnlyDefaultMaterial);

            EditorGUILayout.Separator();
        }

        private void TurnOffRaycasts()
        {
            List<GameObject> alreadyTurnedOff = new List<GameObject>();
            List<GameObject> turnedOffRaycasts = new List<GameObject>();
            List<GameObject> notTurnedOffRaycasts = new List<GameObject>();
            List<GameObject> turnedOffCanvases = new List<GameObject>();
            List<GameObject> turnedOffSkeletons = new List<GameObject>();
            List<GameObject> buttonsTargetGraphic = new List<GameObject>();

            List<Transform> allOptimizeObjects = GetAllOptimizeObjects();
            foreach (Transform optimizeObject in allOptimizeObjects)
            {
                if (optimizeObject.TryGetComponent(out SkeletonGraphic skeletonGraphic))
                {
                    if (skeletonGraphic.raycastTarget == false)
                    {
                        alreadyTurnedOff.Add(skeletonGraphic.gameObject);
                    }
                    else
                    {
                        skeletonGraphic.raycastTarget = false;
                        EditorUtility.SetDirty(skeletonGraphic);
                        turnedOffSkeletons.Add(skeletonGraphic.gameObject);                        
                    }
                }
                
                bool isInteractable = INTERACTABLE_TYPES.Any(type => optimizeObject.TryGetComponent(type, out _));
                bool isButtonTargetGraphic = buttonsTargetGraphic.Contains(optimizeObject.gameObject);
                if (isInteractable || isButtonTargetGraphic)
                {
                    notTurnedOffRaycasts.Add(optimizeObject.gameObject);
                    if (optimizeObject.TryGetComponent(out Button button))
                    {
                        if (button.targetGraphic != null)
                        {
                            buttonsTargetGraphic.Add(button.targetGraphic.gameObject);
                        }
                    }

                    if (optimizeObject.TryGetComponent(out ScrollRect scroll)) 
                    {
                        if (scroll.TryGetComponent(out Image scrollImage) || scrollImage.enabled == false || scrollImage.raycastTarget == false) // Если это скролл без картинки или у самого скролла нет рейкаста
                        {
                            buttonsTargetGraphic.Add(scroll.viewport.gameObject); // то его рейкаст обрабатывает viewport
                        }
                    }
                    
                    CheckElementHasAnyRaycast(optimizeObject.gameObject);
                    continue;
                }
                
                if (optimizeObject.TryGetComponent(out Image childImage))
                {
                    if (childImage.raycastTarget == false)
                    {
                        alreadyTurnedOff.Add(childImage.gameObject);
                    }
                    else
                    {
                        childImage.raycastTarget = false;
                        EditorUtility.SetDirty(childImage);
                        turnedOffRaycasts.Add(childImage.gameObject);
                    }
                }

                if (_objectToOptimize != null && HasCanvasGroups && _optimizeCanvasGroups)
                {
                    if (optimizeObject.TryGetComponent(out CanvasGroup canvasGroup))
                    {
                        Image[] groupImages = canvasGroup.GetComponentsInChildren<Image>();
                        if (canvasGroup.interactable == false)
                        {
                            alreadyTurnedOff.Add(canvasGroup.gameObject);
                        }
                        else
                        {
                            bool hasRaycastElement = groupImages.Any(image => image.raycastTarget);
                            if (!hasRaycastElement)
                            {
                                canvasGroup.interactable = false;
                                canvasGroup.blocksRaycasts = false;
                                EditorUtility.SetDirty(canvasGroup);
                                turnedOffCanvases.Add(canvasGroup.gameObject);
                            }
                        }
                    }
                }
            }

            DoInfoLog("Рейкасты уже отключены у", alreadyTurnedOff);
            DoInfoLog("Отключены рейкасты у Image", turnedOffRaycasts);
            if (_objectToOptimize != null && HasCanvasGroups && _optimizeCanvasGroups) DoInfoLog("Отключены рейкасты у CanvasGroup", turnedOffCanvases); 
            DoInfoLog("Отключены рейкасты у SkeletonGraphics", turnedOffSkeletons);
            DoInfoLog("Не отключены рейкасты у кнопок", notTurnedOffRaycasts);           
        }

        private void SetupUIMaterial()
        {
            List<Image> alreadySetup = new List<Image>();
            List<Image> setupUIMaterial = new List<Image>();
            List<Image> notSetupUIMaterial = new List<Image>();
            
            List<Transform> allOptimizeObjects = GetAllOptimizeObjects();
            foreach (Transform optimizeObject in allOptimizeObjects)
            {
                if (!optimizeObject.TryGetComponent(out Image childImage))
                {
                    continue;
                }

                if (childImage.material == _uiMaterial)
                {
                    alreadySetup.Add(childImage);
                    continue;
                }

                if (_changeOnlyDefaultMaterial && childImage.material.name != DefaultUIMaterialName)
                {
                    notSetupUIMaterial.Add(childImage);
                    continue;
                }
                
                childImage.material = _uiMaterial;
                EditorUtility.SetDirty(childImage);
                setupUIMaterial.Add(childImage);
            }
            
            DoInfoLog("Уже расставлены UIMaterial у", alreadySetup);
            DoInfoLog("Расставлены UIMaterial у", setupUIMaterial);
            DoInfoLog($"Не расставлены UIMaterial, стоит не {DefaultUIMaterialName}", notSetupUIMaterial);
        }

        private void TurnOnRaycasts()
        {
            List<Image> turnedOnRaycasts = new List<Image>();
            
            List<Transform> allOptimizeObjects = GetAllOptimizeObjects();
            foreach (Transform optimizeObject in allOptimizeObjects)
            {
                bool isInteractable = INTERACTABLE_TYPES.Any(type => optimizeObject.TryGetComponent(type, out _));
                if (isInteractable)
                {
                    TryTurnOnRaycast(optimizeObject);
                    if (optimizeObject.TryGetComponent(out Button button))
                    {
                        if (button.targetGraphic != null)
                        {
                            TryTurnOnRaycast(button.targetGraphic.transform);
                        }
                    }
                    
                    CheckElementHasAnyRaycast(optimizeObject.gameObject);
                }
            }
            
            DoInfoLog("Включены рейкасты у", turnedOnRaycasts);

            void TryTurnOnRaycast(Transform transform)
            {
                if (transform.TryGetComponent(out Image image) && image.raycastTarget == false)
                {
                    image.raycastTarget = true;
                    EditorUtility.SetDirty(image);
                    turnedOnRaycasts.Add(image);
                }
            }
        }
        
        /// <summary>
        /// Бывают случаи, когда рейкаст интерактивного элемента обрабатывает не сам элемент, а один из его чайлдов. Для этого выполняется проверка в этом методе.
        /// </summary>
        private void CheckElementHasAnyRaycast(GameObject element) 
        {
            List<Transform> buttonWithChildren = GetAllOptimizeObjects(element);
            bool hasNoRaycastTarget = buttonWithChildren.Select(transform => transform.GetComponent<Image>()).Where(image => image != null).All(image => image.raycastTarget == false || image.enabled == false);
            if (hasNoRaycastTarget) 
            {
                Debug.LogWarning($"Интерактивный элемент {element.name} не имеет рейкастов у себя и у своих чайлдов! Надо включить какой-то вручную! Корень элемента: {element.transform.root.name}");    
            }
        }

        private void FindObjectsShaderUse()
        {
            List<(Transform, Material[])> materialOwners = GetOwnerMaterials();
            List<Transform> usesShaderOwners = new List<Transform>();
            
            foreach ((Transform transform, Material[] materials) in materialOwners)
            {
                foreach (Material material in materials)
                {
                    if (material != null && material.shader != null && !string.IsNullOrEmpty(material.shader.name))
                    {
                        if (material.shader.name.Contains(_findShaderName))
                        {
                            usesShaderOwners.Add(transform);
                        }
                    }
                }
            }

            DoInfoLog("Найден шейдер у", GetRoots(usesShaderOwners));
        }

        private List<(Transform, Material[])> GetOwnerMaterials()
        {
            List<(Transform, Material[])> ownerMaterials = new List<(Transform, Material[])>();
            List<Renderer> renderers = GetAllOptimizeObjects<Renderer>();
            List<Graphic> graphics = GetAllOptimizeObjects<Graphic>();
            
            foreach (Renderer renderer in renderers)
            {
                ownerMaterials.Add((renderer.transform, renderer.sharedMaterials));
            }

            foreach (Graphic graphic in graphics)
            {
                ownerMaterials.Add((graphic.transform, new[] {graphic.material}));
            }

            return ownerMaterials;
        }

        private List<T> GetAllOptimizeObjects<T>() where T : Component
        {
            List<T> optimizeObjects = new List<T>();
            if (!_needOptimizeInFolder) 
            {
                return GetAllOptimizeObjects<T>(_objectToOptimize);
            }
            
            List<string> assetPaths = AssetDatabase.FindAssets("t:Prefab", new [] {_optimizeFolderPath}).Select(AssetDatabase.GUIDToAssetPath).ToList();
            List<GameObject> gameObjectAssets = assetPaths.Select(AssetDatabase.LoadAssetAtPath<GameObject>).ToList();
            Debug.Log($"Найдено префабов в папке: {gameObjectAssets.Count}");
                
            foreach (GameObject gameObjectAsset in gameObjectAssets)
            {
                optimizeObjects.AddRange(GetAllOptimizeObjects<T>(gameObjectAsset));
            }

            return optimizeObjects;   
        }

        private List<Transform> GetAllOptimizeObjects() => GetAllOptimizeObjects<Transform>();

        private List<T> GetAllOptimizeObjects<T>(GameObject root) where T : Component
        {
            List<T> optimizeObjects = new List<T>();
            optimizeObjects.AddRange(root.GetComponentsInChildren<T>(includeInactive: true));
            return optimizeObjects;
        }

        private List<Transform> GetAllOptimizeObjects(GameObject root) => GetAllOptimizeObjects<Transform>(root);

        private void DoInfoLog(string message, IEnumerable<Image> images) =>
            DoInfoLog(message, images.Select(image => image.gameObject));

        private void DoInfoLog(string message, IEnumerable<Transform> transforms) =>
            DoInfoLog(message, transforms.Select(t => t.gameObject));
        
        private void DoInfoLog(string message, IEnumerable<GameObject> gameObjects) =>
            Debug.Log($"{message} ({gameObjects.Count()}): {SplitNames(gameObjects)}");

        private List<Transform> GetRoots(List<Transform> allObjects) =>
            allObjects.Select(obj => obj.root).ToList().Distinct().ToList();

        private string SplitNames(IEnumerable<GameObject> gameObjects) => 
            string.Join(", ", gameObjects.Select(go => go.name));
    }
}
