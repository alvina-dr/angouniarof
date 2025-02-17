using System.Collections.Generic;
using UnityEngine;
using TMPro;
using DG.Tweening;
using UnityEngine.EventSystems;
using System.Linq;

public class Scoreboard : MonoBehaviour
{
    [System.Serializable]
    public class ScoreboardEntry
    {
        public string name;
        public int score;

        public ScoreboardEntry(string _name, int _score)
        {
            name = _name;
            score = _score;
        }
    }

    [System.Serializable]
    public class ScoreboardList
    {
        public List<ScoreboardEntry> entries = new List<ScoreboardEntry>();
    }

    private ScoreboardList scoreList = new ScoreboardList();
    public TMP_InputField inputField;
    public TextMeshProUGUI ScoreText;
    public CanvasGroup typeNameMenu;
    public CanvasGroup scoreboardMenu;
    public GameObject scoreEntryPrefab;
    public Transform scoreEntryLayout;

    private void Start()
    {
        typeNameMenu.alpha = 0;
        typeNameMenu.blocksRaycasts = false;
        scoreboardMenu.alpha = 0;
        scoreboardMenu.blocksRaycasts = false;
        ScoreText.transform.localScale = Vector3.zero;
    }

    public void AddScoreButton()
    {
        AddScoreToScoreboard(inputField.text, GameManager.Instance.CurrentScore);
        typeNameMenu.gameObject.SetActive(false);
        typeNameMenu.alpha = 0;
        typeNameMenu.blocksRaycasts = false;
        ShowScoreboard();
    }

    public void AddScoreToScoreboard(string _name, int _score)
    {
        ScoreboardEntry entry = new(_name, _score);

        if (PlayerPrefs.HasKey("scoreboard"))
        {
            string json = PlayerPrefs.GetString("scoreboard"); // use scoreboard-levelname
            scoreList = JsonUtility.FromJson<ScoreboardList>(json);
        }
        scoreList.entries.Add(entry);
        SaveScoreboard();
    }

    public void ShowTypeNameMenu()
    {
        typeNameMenu.gameObject.SetActive(true);
        typeNameMenu.DOFade(1, .3f).OnComplete(() =>
        {
            typeNameMenu.blocksRaycasts = true;
            ScoreText.text = GameManager.Instance.CurrentScore.ToString();
            ScoreText.transform.DOScale(1.1f, .3f).OnComplete(() =>
            {
                ScoreText.transform.DOScale(1f, .1f);
            });
        });
    }

    private void ShowScoreboard()
    {
        scoreboardMenu.gameObject.SetActive(true);
        scoreboardMenu.blocksRaycasts = true;

        if (PlayerPrefs.HasKey("scoreboard"))
        {
            string json = PlayerPrefs.GetString("scoreboard"); // use scoreboard-levelname
            scoreList = JsonUtility.FromJson<ScoreboardList>(json);
        }

        for (int i = scoreEntryLayout.childCount - 1; i >= 0; i--)
        {
            Destroy(scoreEntryLayout.GetChild(i).gameObject);
        }

        if (scoreList != null)
        {
            for (int i = 0; i < scoreList.entries.Count; i++)
            {
                InstantiateScoreboardEntry(scoreList.entries[i], i);
            }
        }

        scoreboardMenu.DOFade(1, .3f);
    }

    public void HideScoreboard()
    {
        scoreboardMenu.DOFade(0, .3f).OnComplete(() =>
        {
            scoreboardMenu.gameObject.SetActive(false);
            inputField.text = string.Empty;
            GameManager.Instance.ScoreText.SetScore(0);
            typeNameMenu.alpha = 0;
            typeNameMenu.blocksRaycasts = false;
            scoreboardMenu.alpha = 0;
            scoreboardMenu.blocksRaycasts = false;
            ScoreText.transform.localScale = Vector3.zero;
        });
    }

    public void SaveScoreboard()
    {
        scoreList.entries.Sort(SortByScore);
        if (scoreList.entries.Count > GameManager.Instance.ScoreboardSize)
            scoreList.entries = scoreList.entries.Take(GameManager.Instance.ScoreboardSize).ToList();
        string json = JsonUtility.ToJson(scoreList);
        PlayerPrefs.SetString("scoreboard", json);
    }

    public void InstantiateScoreboardEntry(ScoreboardEntry scoreboardEntry, int rank)
    {
        GameObject scoreEntry = Instantiate(scoreEntryPrefab, scoreEntryLayout);
        scoreEntry.transform.GetChild(0).GetComponent<TextMeshProUGUI>().text = (rank + 1).ToString();
        scoreEntry.transform.GetChild(1).GetComponent<TextMeshProUGUI>().text = scoreboardEntry.name;
        scoreEntry.transform.GetChild(2).GetComponent<TextMeshProUGUI>().text = scoreboardEntry.score.ToString();
    }

    static int SortByScore(ScoreboardEntry p1, ScoreboardEntry p2)
    {
        return -p1.score.CompareTo(p2.score);
    }
}