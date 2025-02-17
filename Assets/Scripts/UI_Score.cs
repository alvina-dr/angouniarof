using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class UI_Score : MonoBehaviour
{
    [SerializeField]
    private List<ColorLevel> _colorLevelList = new List<ColorLevel>();
    [SerializeField]
    private TextMeshProUGUI _scoreText;

    public void SetScore(float score)
    {
        _scoreText.text = score.ToString();
        for (int i = 0; i < _colorLevelList.Count; i++)
        {
            if (score > _colorLevelList[i].MinScore)
            {
                _scoreText.color = _colorLevelList[i].Color;
                break;
            }
        }
    }

    [System.Serializable]
    public class ColorLevel
    {
        public Color Color;
        public float MinScore;
    }
}
