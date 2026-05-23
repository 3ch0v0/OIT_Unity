using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class OITTestController : MonoBehaviour
{
    [System.Serializable]
    public struct ModelData
    {
        public string ModelName;
        
        public GameObject[] AlgorithmObjects; 
    }

    [Header("UI")]
    public TMP_Dropdown algorithmDropdown;
    public TMP_Dropdown modelDropdown;

    [Header("Layers UI")]
    public GameObject peelingSettingsPanel; 
    public Slider peelLayerSlider;          
    public TextMeshProUGUI peelLayerText;   

    [Header("Algorithm Names")]
    public List<string> algorithmNames = new List<string>() 
    { 
        "WBOIT", 
        "Depth Peeling", 
        "A-Buffer", 
        "None" ,
        "K-Buffer"
    };

    [Tooltip("Index setting")]
    public int depthPeelingIndex = 1; 
    public int KbufferIndex = 4;

    [Header("Model Setting")]
    public List<ModelData> models = new List<ModelData>();

  
    private GameObject m_CurrentActiveObject;

    private void Start()
    {
        if (algorithmDropdown == null || modelDropdown == null)
        {
            Debug.LogError("OITTestController: null Dropdown ");
            return;
        }

      
        InitializeAllObjects();

       
        algorithmDropdown.ClearOptions();
        algorithmDropdown.AddOptions(algorithmNames);
        algorithmDropdown.onValueChanged.AddListener(OnSelectionChanged);
        modelDropdown.ClearOptions();
        List<string> modelNames = new List<string>();
        foreach (var m in models)
        {
            modelNames.Add(m.ModelName);
        }
        modelDropdown.AddOptions(modelNames);
        modelDropdown.onValueChanged.AddListener(OnSelectionChanged);
        
        if (peelLayerSlider != null)
        {
            peelLayerSlider.minValue = 1;
            peelLayerSlider.maxValue = 8;
            peelLayerSlider.wholeNumbers = true;
           
            peelLayerSlider.value = OITRegistry.Layers; 
            peelLayerSlider.onValueChanged.AddListener(OnSliderValueChanged);
            UpdateSliderText((int)peelLayerSlider.value);
        }

     
        OnSelectionChanged(0);
    }

    private void InitializeAllObjects()
    {
        foreach (var model in models)
        {
            foreach (var obj in model.AlgorithmObjects)
            {
                if (obj != null) obj.SetActive(false);
            }
        }
    }
    
    private void OnSelectionChanged(int ignoredIndex)
    {
        int currentAlgo = algorithmDropdown.value;
        int currentModel = modelDropdown.value;
        
        if (currentModel >= models.Count || currentAlgo >= models[currentModel].AlgorithmObjects.Length)
            return;
        
        GameObject newObject = models[currentModel].AlgorithmObjects[currentAlgo];
        
        if (newObject == m_CurrentActiveObject)
        {
            UpdateUI(currentAlgo);
            return;
        }
        
        if (m_CurrentActiveObject != null)
        {
            m_CurrentActiveObject.SetActive(false);
        }
        
        if (newObject != null)
        {
            newObject.SetActive(true);
        }
        
        m_CurrentActiveObject = newObject;

       
        UpdateUI(currentAlgo);
    }

    private void UpdateUI(int currentAlgo)
    {
        if (peelingSettingsPanel != null)
        {
            peelingSettingsPanel.SetActive(currentAlgo == depthPeelingIndex||currentAlgo==KbufferIndex);
            
        }
    }

    private void OnSliderValueChanged(float value)
    {
        int layers = (int)value;
        OITRegistry.Layers = layers;
        UpdateSliderText(layers);
    }

    private void UpdateSliderText(int layers)
    {
        if (peelLayerText != null)
        {
            peelLayerText.text = "Layers: " + layers;
        }
    }

    private void OnDestroy()
    {
        if (algorithmDropdown != null) algorithmDropdown.onValueChanged.RemoveListener(OnSelectionChanged);
        if (modelDropdown != null) modelDropdown.onValueChanged.RemoveListener(OnSelectionChanged);
        if (peelLayerSlider != null) peelLayerSlider.onValueChanged.RemoveListener(OnSliderValueChanged);
    }
}