﻿using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor.Experimental.EditorVR;
using UnityEditor.Experimental.EditorVR.Extensions;
using UnityEditor.Experimental.EditorVR.Utilities;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.UI;
using SpatialinterfaceState = UnityEditor.Experimental.EditorVR.SpatialInterfaceController.SpatialinterfaceState;

public class SpatialInterfaceUI : MonoBehaviour
{
    readonly string k_TranslationInputModeName = "Translation Input Mode";
    readonly string k_RayBasedInputModeName = "Ray-based Input Mode";
    readonly string k_RotationInputModeName = "Rotation Input Mode";
    readonly string k_BCIInputModeName = "Brain Input Mode";

    public enum SpatialInterfaceInputMode
    {
        Translation,
        Rotation,
        Ray,
        BCI
    }

    [Header("Common UI")]
    [SerializeField]
    CanvasGroup m_MainCanvasGroup;

    [SerializeField]
    Transform m_Background;

    [SerializeField]
    TextMeshProUGUI m_MenuTitleText;

    [SerializeField]
    TextMeshProUGUI m_InputModeText;

    [Header("Home Section")]
    [SerializeField]
    Transform m_HomeMenuContainer;

    [SerializeField]
    CanvasGroup m_HomeTextCanvasGroup;

    [SerializeField]
    HorizontalLayoutGroup m_HomeMenuLayoutGroup;

    [SerializeField]
    Transform m_HomeTextBackgroundTransform;

    [SerializeField]
    Transform m_HomeTextBackgroundInnerTransform;

    [SerializeField]
    TextMeshProUGUI m_HomeSectionDescription;

    [SerializeField]
    List<TextMeshProUGUI> m_SectionNameTexts = new List<TextMeshProUGUI>();

    [SerializeField]
    CanvasGroup m_HomeSectionBackgroundBordersCanvas;

    [Header("SubMenu Section")]
    [SerializeField]
    Transform m_SubMenuContainer;

    [SerializeField]
    CanvasGroup m_SubMenuContentsCanvasGroup;

    [Header("Prefabs")]
    [SerializeField]
    GameObject m_MenuElementPrefab;

    [Header("Animation")]
    [SerializeField]
    PlayableDirector m_Director;

    [SerializeField]
    PlayableAsset m_RevealTimelinePlayable;

    [SerializeField]
    GameObject m_SubMenuElementPrefab;

    [Header("Ghost Input Device")]
    [SerializeField]
    SpatialUIGhostVisuals m_SpatialUIGhostVisuals;

    [Header("Surrounding Arrows")]
    [SerializeField]
    Transform m_SurroundingArrowsContainer;

    bool m_Visible;
    SpatialInterfaceInputMode m_SpatialInterfaceInputMode;
    SpatialinterfaceState m_SpatialinterfaceState;

    Vector3 m_HomeTextBackgroundOriginalLocalScale;
    Vector3 m_HomeBackgroundOriginalLocalScale;
    float m_HomeSectionTimelineDuration;
    float m_HomeSectionTimelineStoppingTime;

    // Adaptive Position related fields
    bool m_InFocus;
    bool m_BeingMoved;

    Coroutine m_VisibilityCoroutine;
    Coroutine m_InFocusCoroutine;
    Coroutine m_HomeSectionTitlesBackgroundBordersTransitionCoroutine;

    // Reference set by the controller in the Setup method
    Dictionary<ISpatialMenuProvider, SpatialUIMenuElement> m_ProviderToMenuElements;

    readonly List<SpatialUIMenuElement> currentlyDisplayedMenuElements = new List<SpatialUIMenuElement>();

    private bool visible
    {
        get { return m_Visible; }

        set
        {
            if (m_Visible == value)
                return;

            m_Visible = value;

            if (m_Visible)
            {
                gameObject.SetActive(true);
            }
            else
            {
                //
            }
        }
    }

    public ISpatialMenuProvider highlightedTopLevelMenuProvider { private get; set; }
    public SpatialinterfaceState spatialinterfaceState
    {
        set { m_SpatialinterfaceState = value; }
    }

    public bool directorBeyondHomeSectionDuration
    {
        get { return m_Director.time > m_HomeSectionTimelineDuration; }
    }

    public SpatialInterfaceInputMode spatialInterfaceInputMode
    {
        set
        {
            if (m_SpatialInterfaceInputMode == value)
                return;

            m_SpatialInterfaceInputMode = value;

            switch (m_SpatialInterfaceInputMode)
            {
                case SpatialInterfaceInputMode.Translation:
                    m_InputModeText.text = k_TranslationInputModeName;
                    m_SpatialUIGhostVisuals.spatialInteractionType = SpatialUIGhostVisuals.SpatialInteractionType.touch;
                    break;
                case SpatialInterfaceInputMode.Ray:
                    m_InputModeText.text = k_RayBasedInputModeName;
                    m_SpatialUIGhostVisuals.spatialInteractionType = SpatialUIGhostVisuals.SpatialInteractionType.ray;
                    break;
                case SpatialInterfaceInputMode.BCI:
                    m_InputModeText.text = k_BCIInputModeName;
                    m_SpatialUIGhostVisuals.spatialInteractionType = SpatialUIGhostVisuals.SpatialInteractionType.bci;
                    break;
            }
        }
    }

    public bool inFocus
    {
        set
        {
            if (m_InFocus == value)
                return;

            m_InFocus = value;
        }
    }

    public bool beingMoved
    {
        set
        {
            if (m_BeingMoved == value)
                return;

            m_BeingMoved = value;
            this.RestartCoroutine(ref m_VisibilityCoroutine, AnimateVisibility());
        }
    }

    public Transform homeMenuContainer { get { return m_HomeMenuContainer; } }

    public GameObject menuElementPrefab { get { return m_MenuElementPrefab; } }

    public GameObject subMenuElementPrefab { get { return m_SubMenuElementPrefab; } }

    public Transform subMenuContainer { get { return m_SubMenuContainer; } }

    public void Setup(Dictionary<ISpatialMenuProvider, SpatialUIMenuElement> providerToMenuElementsx)
    {
        m_ProviderToMenuElements = providerToMenuElementsx;
        m_HomeTextBackgroundOriginalLocalScale = m_HomeTextBackgroundTransform.localScale;
        m_HomeBackgroundOriginalLocalScale = m_Background.localScale;

        // TODO remove serialized inspector references for home menu section titles, use instantiated prefabs only
        m_SectionNameTexts.Clear();

        m_HomeSectionTimelineDuration = (float) m_RevealTimelinePlayable.duration;
        m_HomeSectionTimelineStoppingTime = m_HomeSectionTimelineDuration * 0.5f;
    }

    public void Reset()
    {
        m_InputModeText.text = k_TranslationInputModeName;
        m_Director.playableAsset = m_RevealTimelinePlayable;
        m_HomeSectionBackgroundBordersCanvas.alpha = 1f;

        DisplayHomeSectionContents();

        // Director related
        m_Director.time = 0f;
        m_Director.Evaluate();

        // Hack that fixes the home section menu element positions not being recalculated when first revealed
        m_HomeMenuLayoutGroup.enabled = false;
        m_HomeMenuLayoutGroup.enabled = true;
        m_SpatialUIGhostVisuals.spatialInteractionType = SpatialUIGhostVisuals.SpatialInteractionType.touch;
    }

    /*
    void UpdateX()
    {
        UpdateDirector();
    }
    */

    void Update()
    {
        //Debug.Log("<color=yellow>" + m_Transitioning + "</color>");
        if (m_SpatialinterfaceState == SpatialinterfaceState.hidden && m_Director.time <= m_HomeSectionTimelineDuration)
        {
            m_Director.time = m_Director.time += Time.unscaledDeltaTime;
            m_Director.Evaluate();

            m_SubMenuContentsCanvasGroup.alpha = Mathf.Clamp01(m_SubMenuContentsCanvasGroup.alpha - Time.unscaledDeltaTime * 4);
            m_HomeSectionBackgroundBordersCanvas.alpha = Mathf.Clamp01(m_HomeSectionBackgroundBordersCanvas.alpha - Time.unscaledDeltaTime * 4);
        }
        else if (m_Director.time > m_HomeSectionTimelineDuration)
        {
            //m_Director.time = 0f;
            m_HomeTextBackgroundInnerTransform.localScale = new Vector3(1f, 1f, 1f);
            m_SubMenuContentsCanvasGroup.alpha = 0f;

            this.StopAllCoroutines();
            //HideSubMenu();
            m_Director.Evaluate();
            gameObject.SetActive(m_Visible);

            var deleteOldChildren = m_SubMenuContainer.GetComponentsInChildren<Transform>().Where((x) => x != m_SubMenuContainer);
            if (deleteOldChildren.Count() > 0)
            {
                foreach (var child in deleteOldChildren)
                {
                    if (child != null && child.gameObject != null)
                        ObjectUtils.Destroy(child.gameObject);
                }
            }
        }
        else if (m_SpatialinterfaceState == SpatialinterfaceState.navigatingSubMenuContent)
        {
            // Scale background based on number of sub-menu elements
            var targetScale = highlightedTopLevelMenuProvider != null ? highlightedTopLevelMenuProvider.spatialTableElements.Count * 1.05f : 1f;
            var timeMultiplier = 24;
            if (m_HomeTextBackgroundInnerTransform.localScale.y < targetScale)
            {
                if (m_HomeTextBackgroundInnerTransform.localScale.y + Time.unscaledDeltaTime * timeMultiplier > targetScale)
                {
                    m_HomeTextBackgroundInnerTransform.localScale = new Vector3(1f, targetScale, 1f);
                    m_SubMenuContentsCanvasGroup.alpha = 1f;
                    return;
                }

                var newScale = new Vector3(m_HomeTextBackgroundInnerTransform.localScale.x, m_HomeTextBackgroundInnerTransform.localScale.y + Time.unscaledDeltaTime * timeMultiplier, m_HomeTextBackgroundInnerTransform.localScale.z);
                m_HomeTextBackgroundInnerTransform.localScale = newScale;
                m_SubMenuContentsCanvasGroup.alpha += Time.unscaledDeltaTime;
            }
            else
            {
                return;
                m_HomeTextBackgroundInnerTransform.localScale = new Vector3(1f, targetScale, 1f);
                m_SubMenuContentsCanvasGroup.alpha = 1f;
            }
        }
        else if (m_SpatialinterfaceState == SpatialinterfaceState.navigatingTopLevel)
        {
            var targetScale = 1f;
            var timeMultiplier = 24;
            if (m_HomeTextBackgroundInnerTransform.localScale.y > targetScale)
            {
                if (m_HomeTextBackgroundInnerTransform.localScale.y - Time.unscaledDeltaTime * timeMultiplier < targetScale)
                {
                    m_HomeTextBackgroundInnerTransform.localScale = new Vector3(1f, targetScale, 1f);
                    m_SubMenuContentsCanvasGroup.alpha = 0f;
                    var deleteOldChildren = m_SubMenuContainer.GetComponentsInChildren<Transform>().Where((x) => x != m_SubMenuContainer);
                    if (deleteOldChildren.Count() > 0)
                    {
                        foreach (var child in deleteOldChildren)
                        {
                            if (child != null && child.gameObject != null)
                                ObjectUtils.Destroy(child.gameObject);
                        }
                    }
                    return;
                }

                var newScale = new Vector3(m_HomeTextBackgroundInnerTransform.localScale.x, m_HomeTextBackgroundInnerTransform.localScale.y - Time.unscaledDeltaTime * timeMultiplier, m_HomeTextBackgroundInnerTransform.localScale.z);
                m_HomeTextBackgroundInnerTransform.localScale = newScale;
                m_SubMenuContentsCanvasGroup.alpha -= Time.unscaledDeltaTime * 10;
            }
            else
            {
                //m_HomeTextBackgroundInnerTransform.localScale = new Vector3(1f, targetScale, 1f);
                //m_SubMenuContentsCanvasGroup.alpha = 0f;


            }
        }
    }

    public void UpdateGhostDeviceRotation(Quaternion newRotation)
    {
        m_SpatialUIGhostVisuals.UpdateRotation(newRotation);
    }

    public void UpdateDirector()
    {
        if (m_Director.time <= m_HomeSectionTimelineStoppingTime)
        {
            m_Director.time = m_Director.time += Time.unscaledDeltaTime;
            m_Director.Evaluate();
        }
    }

    public void UpdateSectionNames(List<ISpatialMenuProvider> spatialMenuProviders)
    {
        for (int i = 0; i < spatialMenuProviders.Count; ++i)
        {
            m_SectionNameTexts[i].text = spatialMenuProviders[i].spatialMenuName;
        }
    }

    public void DisplayHomeSectionContents()
    {
        m_SpatialUIGhostVisuals.SetPositionOffset(Vector3.zero);
        m_SpatialUIGhostVisuals.spatialInteractionType = SpatialUIGhostVisuals.SpatialInteractionType.touch;
        this.RestartCoroutine(ref m_HomeSectionTitlesBackgroundBordersTransitionCoroutine, AnimateTopAndBottomCenterBackgroundBorders(true));

        spatialinterfaceState = SpatialinterfaceState.navigatingTopLevel;

        // Proxy sub-menu/dynamicHUD menu element(s) display
        m_HomeTextBackgroundTransform.localScale = m_HomeTextBackgroundOriginalLocalScale;
        m_HomeSectionDescription.gameObject.SetActive(true);

        foreach (var kvp in m_ProviderToMenuElements)
        {
            var elementTransform = kvp.Value.transform;
            elementTransform.gameObject.SetActive(true);
            elementTransform.localScale = Vector3.one;
        }
    }

    public void DisplayHighlightedSubMenuContents()
    {
        const float subMenuElementHeight = 0.022f; // TODO source height from individual sub-menu element height, not arbitrary value
        int subMenuElementCount = 0;
        foreach (var kvp in m_ProviderToMenuElements)
        {
            var key = kvp.Key;
            if (key == highlightedTopLevelMenuProvider)
            {
                // m_SubMenuText.text = m_HighlightedTopLevelMenuProvider.spatialTableElements[0].name;
                // TODO display all sub menu contents here

                currentlyDisplayedMenuElements.Clear();
                var deleteOldChildren = m_SubMenuContainer.GetComponentsInChildren<Transform>().Where( (x) => x != m_SubMenuContainer);
                foreach (var child in deleteOldChildren)
                {
                    if (child != null && child.gameObject != null)
                        ObjectUtils.Destroy(child.gameObject);
                }

                foreach (var subMenuElement in highlightedTopLevelMenuProvider.spatialTableElements)
                {
                    ++subMenuElementCount;
                    var instantiatedPrefab = ObjectUtils.Instantiate(subMenuElementPrefab).transform as RectTransform;
                    var providerMenuElement = instantiatedPrefab.GetComponent<SpatialUIMenuElement>();
                    providerMenuElement.Setup(instantiatedPrefab, subMenuContainer, () => Debug.Log("Setting up SubMenu : " + subMenuElement.name), subMenuElement.name);
                    currentlyDisplayedMenuElements.Add(providerMenuElement);
                }

                //.Add(provider, providerMenuElement);
                //instantiatedPrefab.transform.SetParent(m_SubMenuContainer);
                //instantiatedPrefab.localRotation = Quaternion.identity;
                //instantiatedPrefab.localPosition = Vector3.zero;
                //instantiatedPrefab.localScale = Vector3.one;
            }

            kvp.Value.gameObject.SetActive(false);
        }

        var newGhostInputDevicePositionOffset = new Vector3(0f, subMenuElementHeight * subMenuElementCount, 0f);
        m_SpatialUIGhostVisuals.SetPositionOffset(newGhostInputDevicePositionOffset);
        m_HomeSectionDescription.gameObject.SetActive(false);
        this.RestartCoroutine(ref m_HomeSectionTitlesBackgroundBordersTransitionCoroutine, AnimateTopAndBottomCenterBackgroundBorders(false));

    }

    public void HighlightHomeSectionMenuElement(ISpatialMenuProvider provider)
    {
        m_HomeSectionDescription.text = provider.spatialMenuDescription;
        highlightedTopLevelMenuProvider = provider;

        foreach (var kvp in m_ProviderToMenuElements)
        {
            var key = kvp.Key;
            var targetSize = key == provider ? Vector3.one : Vector3.one * 0.5f;
            kvp.Value.transform.localScale = targetSize;
        }
    }

    public void HighlightSingleElementInCurrentMenu(int elementOrderPosition)
    {
        var menuElementCount = highlightedTopLevelMenuProvider.spatialTableElements.Count;
        for (int i = 0; i < menuElementCount; ++i)
        {

            //var x = m_ProviderToMenuElements[m_HighlightedTopLevelMenuProvider];
            currentlyDisplayedMenuElements[i].highlighted = i == elementOrderPosition;
            //m_HighlightedTopLevelMenuProvider.spatialTableElements[i].name = i == highlightedButtonPosition ? "Highlighted" : "Not";
        }
    }

    IEnumerator AnimateFocusVisuals()
    {
        var currentScale = transform.localScale;
        var targetScale = m_InFocus ? Vector3.one : Vector3.one * 0.5f;
        var transitionAmount = 0f; // this should account for the magnitude difference between the highlightedYPositionOffset, and the current magnitude difference between the local Y and the original Y
        var transitionSubtractMultiplier = 5f;
        while (transitionAmount < 1f)
        {
            var smoothTransition = MathUtilsExt.SmoothInOutLerpFloat(transitionAmount);
            transform.localScale = Vector3.Lerp(currentScale, targetScale, smoothTransition);
            transitionAmount += Time.deltaTime * transitionSubtractMultiplier;
            yield return null;
        }

        transform.localScale = targetScale;
        m_InFocusCoroutine = null;
    }

    IEnumerator AnimateVisibility()
    {
        var speedScalar = m_BeingMoved ? 2f : 4f;
        var currentAlpha = m_MainCanvasGroup.alpha;
        var targetMainCanvasAlpha = m_BeingMoved ? 0.25f : 1f;

        var currentHomeTextAlpha = m_HomeTextCanvasGroup.alpha;
        var targetHomeTextAlpha = m_BeingMoved ? 0f : 1f;

        //var currentBackgroundLocalScale = m_Background.localScale;
        //var targetBackgroundLocalScale = Vector3.one * (m_BeingMoved ? 0.75f : 1f);

        var currentHomeBackgroundLocalScale = m_HomeTextBackgroundTransform.localScale;
        var targetHomeBackgroundLocalScale = m_BeingMoved ? new Vector3(m_HomeTextBackgroundOriginalLocalScale.x, 0f, 1f) : m_HomeTextBackgroundOriginalLocalScale;
        //var targetPosition = show ? (moveToAlternatePosition ? m_AlternateLocalPosition : m_OriginalLocalPosition) : Vector3.zero;
        //var targetScale = show ? (moveToAlternatePosition ? m_OriginalLocalScale : m_OriginalLocalScale * k_AlternateLocalScaleMultiplier) : Vector3.zero;
        //var currentPosition = transform.localPosition;
        //var currentIconScale = m_IconContainer.localScale;
        //var targetIconContainerScale = show ? m_OriginalIconContainerLocalScale : Vector3.zero;
        var transitionAmount = 0f;
        //var currentScale = transform.localScale;

        if (!m_BeingMoved)
        {
            var delayBeforeReveal = 0.5f;
            while (delayBeforeReveal > 0)
            {
                // Pause before revealing
                delayBeforeReveal -= Time.unscaledDeltaTime;
                yield return null;
            }
        }

        /*
        while (transitionAmount < 1)
        {
            var shapedAmount = MathUtilsExt.SmoothInOutLerpFloat(transitionAmount += Time.unscaledDeltaTime * speedScalar);
            //m_Director.time = shapedAmount;
            //m_IconContainer.localScale = Vector3.Lerp(currentIconScale, targetIconContainerScale, shapedAmount);
            //transform.localPosition = Vector3.Lerp(currentPosition, targetPosition, shapedAmount);
            //transform.localScale = Vector3.Lerp(currentScale, targetScale, shapedAmount);
            //m_Background.localScale = Vector3.Lerp(currentBackgroundLocalScale, targetBackgroundLocalScale, shapedAmount);

            m_MainCanvasGroup.alpha = Mathf.Lerp(currentAlpha, targetMainCanvasAlpha, shapedAmount);

            shapedAmount *= shapedAmount; // increase beginning & end anim emphasis
            m_HomeTextCanvasGroup.alpha = Mathf.Lerp(currentHomeTextAlpha, targetHomeTextAlpha, shapedAmount);

            m_HomeTextBackgroundTransform.localScale = Vector3.Lerp(currentHomeBackgroundLocalScale, targetHomeBackgroundLocalScale, shapedAmount);
            yield return null;
        }

        //m_IconContainer.localScale = targetIconContainerScale;
        //transform.localScale = targetScale;
        //transform.localPosition = targetPosition;

        m_MainCanvasGroup.alpha = targetMainCanvasAlpha;
        */
        //m_Background.localScale = targetBackgroundLocalScale;

        m_VisibilityCoroutine = null;
    }

    IEnumerator AnimateTopAndBottomCenterBackgroundBorders(bool visible)
    {
        var currentAlpha = m_HomeSectionBackgroundBordersCanvas.alpha;
        var targetAlpha = visible ? 1f : 0f;
        var transitionAmount = 0f;
        var transitionSubtractMultiplier = 5f;
        while (transitionAmount < 1f)
        {
            var smoothTransition = MathUtilsExt.SmoothInOutLerpFloat(transitionAmount);
            m_HomeSectionBackgroundBordersCanvas.alpha = Mathf.Lerp(currentAlpha, targetAlpha, smoothTransition);
            m_SurroundingArrowsContainer.localScale = Vector3.one + (Vector3.one * Mathf.Sin(transitionAmount * 2) * 0.1f);
            transitionAmount += Time.deltaTime * transitionSubtractMultiplier;
            yield return null;
        }

        m_HomeSectionBackgroundBordersCanvas.alpha = targetAlpha;
        m_HomeSectionTitlesBackgroundBordersTransitionCoroutine = null;
    }
}
