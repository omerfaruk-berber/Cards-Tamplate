using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MultiObjectYSlider : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Slider slider;
    [SerializeField] private List<Transform> targets = new List<Transform>();

    [Header("Auto Find By Name")]
    [SerializeField] private bool autoFindByName = false;
    [SerializeField] private bool includeInactive = true;
    [SerializeField] private List<string> targetNames = new List<string>();

    [Header("Step Mode (UI friendly)")]
    [Tooltip("Slider value * stepPixels kadar Y ekseninde asagi tasir.")]
    [SerializeField] private float stepPixels = 20f;

    private readonly List<Vector3> basePositions = new List<Vector3>();
    private float defaultSliderValue;

    private void Awake()
    {
        if (slider == null)
        {
            slider = GetComponent<Slider>();
        }

        if (slider != null)
        {
            defaultSliderValue = slider.value;
        }

        if (autoFindByName)
        {
            ResolveTargetsByName();
        }

        CacheBasePositions();
    }

    private void OnEnable()
    {
        if (slider != null)
        {
            slider.onValueChanged.AddListener(HandleSliderChanged);
            HandleSliderChanged(slider.value);
        }
    }

    private void OnDisable()
    {
        if (slider != null)
        {
            slider.onValueChanged.RemoveListener(HandleSliderChanged);
        }
    }

    private void OnValidate()
    {
        if (slider == null)
        {
            slider = GetComponent<Slider>();
        }
    }

    private void CacheBasePositions()
    {
        basePositions.Clear();
        for (int i = 0; i < targets.Count; i++)
        {
            var target = targets[i];
            if (target == null)
            {
                basePositions.Add(Vector3.zero);
                continue;
            }

            if (target is RectTransform rect)
            {
                Vector2 anchored = rect.anchoredPosition;
                basePositions.Add(new Vector3(anchored.x, anchored.y, 0f));
            }
            else
            {
                basePositions.Add(target.position);
            }
        }
    }

    public void ResolveTargetsByName()
    {
        targets.Clear();

        for (int i = 0; i < targetNames.Count; i++)
        {
            string targetName = targetNames[i];
            if (string.IsNullOrWhiteSpace(targetName))
            {
                continue;
            }

            Transform found = FindTransformInLoadedScenes(targetName, includeInactive);
            if (found != null)
            {
                targets.Add(found);
            }
        }
    }

    private static Transform FindTransformInLoadedScenes(string targetName, bool includeInactive)
    {
        int sceneCount = SceneManager.sceneCount;
        for (int i = 0; i < sceneCount; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);
            if (!scene.isLoaded)
            {
                continue;
            }

            GameObject[] roots = scene.GetRootGameObjects();
            for (int r = 0; r < roots.Length; r++)
            {
                Transform[] all = roots[r].GetComponentsInChildren<Transform>(includeInactive);
                for (int t = 0; t < all.Length; t++)
                {
                    if (all[t].name == targetName)
                    {
                        return all[t];
                    }
                }
            }
        }

        return null;
    }

    private void HandleSliderChanged(float value)
    {
        if (slider == null)
        {
            return;
        }

        // Slider value 0..10 => move down by value * stepPixels.
        float yValue = -value * stepPixels;

        for (int i = 0; i < targets.Count; i++)
        {
            var target = targets[i];
            if (target == null)
            {
                continue;
            }

            if (target is RectTransform rect)
            {
                Vector2 basePos = new Vector2(basePositions[i].x, basePositions[i].y);
                basePos.y = basePositions[i].y + yValue;
                rect.anchoredPosition = basePos;
            }
            else
            {
                Vector3 pos = basePositions[i];
                pos.y = basePositions[i].y + yValue;
                target.position = pos;
            }
        }
    }

    public void RefreshBasePositions()
    {
        CacheBasePositions();
        if (slider != null)
        {
            HandleSliderChanged(slider.value);
        }
    }

    public void ResetToDefault()
    {
        if (slider == null)
        {
            return;
        }

        slider.SetValueWithoutNotify(defaultSliderValue);
        HandleSliderChanged(slider.value);
    }
}
