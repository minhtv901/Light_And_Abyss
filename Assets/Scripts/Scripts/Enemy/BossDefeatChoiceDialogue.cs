using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public enum BossDialogueRouteBias
{
    None,
    Purified,
    Dark
}

[System.Serializable]
public class BossDialogueChoice
{
    [TextArea] public string choiceText;
    public int darkPoint;
    public int purifiedPoint;

    [Tooltip("Used only when final scores are equal. For balance choices, keep this as None.")]
    public BossDialogueRouteBias tieBreakerBias = BossDialogueRouteBias.None;
}

[System.Serializable]
public class BossDialogueStep
{
    public string speakerName = "Boss";
    [TextArea] public string dialogueText;
    public BossDialogueChoice[] choices = new BossDialogueChoice[3];
}

public class BossDefeatChoiceDialogue : MonoBehaviour
{
    [Header("UI")]
    public GameObject dialoguePanel;
    public TMP_Text speakerText;
    public TMP_Text dialogueText;
    public Button[] choiceButtons = new Button[3];
    public TMP_Text[] choiceButtonTexts = new TMP_Text[3];

    [Header("Result UI")]
    public bool showResultPanel = true;
    public GameObject resultPanel;
    public TMP_Text resultText;
    public float resultShowDuration = 1.2f;

    [Header("Dialogue Steps")]
    public BossDialogueStep[] steps;

    [Header("References")]
    public PlayerMovement playerMovement;
    public GameObject bossObjectToDisable;
    public GameObject gateToOpen;

    [Header("Options")]
    public bool pauseGameWhileChoosing = true;
    public bool hideBossAfterChoice = true;
    public bool openGateAfterChoice = true;
    public bool debugLog = true;

    private int currentStepIndex;
    private int darkScore;
    private int purifiedScore;
    private BossDialogueRouteBias lastNonNeutralBias = BossDialogueRouteBias.None;
    private bool isDialogueRunning;

    public int DarkScore => darkScore;
    public int PurifiedScore => purifiedScore;

    private void Awake()
    {
        HideDialogueUI();

        if (dialoguePanel != null)
            dialoguePanel.SetActive(false);

        if (resultPanel != null)
            resultPanel.SetActive(false);

        for (int i = 0; i < choiceButtons.Length; i++)
        {
            int index = i;

            if (choiceButtons[i] != null)
                choiceButtons[i].onClick.AddListener(() => SelectChoice(index));
        }
    }

    private void HideDialogueUI()
    {
        if (dialoguePanel != null)
            dialoguePanel.SetActive(false);

        if (resultPanel != null)
            resultPanel.SetActive(false);

        for (int i = 0; i < choiceButtons.Length; i++)
        {
            if (choiceButtons[i] != null)
                choiceButtons[i].gameObject.SetActive(false);
        }
    }

    private void Reset()
    {
        steps = new BossDialogueStep[4];

        steps[0] = new BossDialogueStep
        {
            speakerName = "Boss",
            dialogueText = "Ngươi đã chạm tới sức mạnh thật sự... nhưng ngươi sẽ dùng nó thế nào?",
            choices = new BossDialogueChoice[]
            {
                new BossDialogueChoice { choiceText = "Ta sẽ dùng nó để kết thúc mọi thứ.", darkPoint = 1, purifiedPoint = 0, tieBreakerBias = BossDialogueRouteBias.Dark },
                new BossDialogueChoice { choiceText = "Ta sẽ dùng nó để bảo vệ những người còn sống.", darkPoint = 0, purifiedPoint = 1, tieBreakerBias = BossDialogueRouteBias.Purified },
                new BossDialogueChoice { choiceText = "Ta sẽ tự quyết định cách dùng sức mạnh này.", darkPoint = 1, purifiedPoint = 1, tieBreakerBias = BossDialogueRouteBias.None }
            }
        };

        steps[1] = new BossDialogueStep
        {
            speakerName = "Boss",
            dialogueText = "Ánh sáng yếu đuối. Bóng tối mới là thứ giúp ngươi sống sót.",
            choices = new BossDialogueChoice[]
            {
                new BossDialogueChoice { choiceText = "Vậy ta sẽ nắm lấy bóng tối.", darkPoint = 1, purifiedPoint = 0, tieBreakerBias = BossDialogueRouteBias.Dark },
                new BossDialogueChoice { choiceText = "Không. Sức mạnh không cần phải mục ruỗng.", darkPoint = 0, purifiedPoint = 1, tieBreakerBias = BossDialogueRouteBias.Purified },
                new BossDialogueChoice { choiceText = "Cả hai đều chỉ là công cụ.", darkPoint = 1, purifiedPoint = 1, tieBreakerBias = BossDialogueRouteBias.None }
            }
        };

        steps[2] = new BossDialogueStep
        {
            speakerName = "Boss",
            dialogueText = "Theo ta. Ta sẽ cho ngươi quyền năng vượt qua mọi giới hạn.",
            choices = new BossDialogueChoice[]
            {
                new BossDialogueChoice { choiceText = "Nếu đó là cái giá để mạnh hơn, ta chấp nhận.", darkPoint = 1, purifiedPoint = 0, tieBreakerBias = BossDialogueRouteBias.Dark },
                new BossDialogueChoice { choiceText = "Ta sẽ không đánh mất chính mình.", darkPoint = 0, purifiedPoint = 1, tieBreakerBias = BossDialogueRouteBias.Purified },
                new BossDialogueChoice { choiceText = "Ta sẽ lấy sức mạnh, nhưng không theo ngươi.", darkPoint = 1, purifiedPoint = 1, tieBreakerBias = BossDialogueRouteBias.None }
            }
        };

        steps[3] = new BossDialogueStep
        {
            speakerName = "Boss",
            dialogueText = "Vậy hãy chọn đi. Ngươi sẽ bước tiếp bằng ánh sáng... hay bóng tối?",
            choices = new BossDialogueChoice[]
            {
                new BossDialogueChoice { choiceText = "Ta chọn bóng tối.", darkPoint = 1, purifiedPoint = 0, tieBreakerBias = BossDialogueRouteBias.Dark },
                new BossDialogueChoice { choiceText = "Ta chọn thanh tẩy.", darkPoint = 0, purifiedPoint = 1, tieBreakerBias = BossDialogueRouteBias.Purified }
            }
        };
    }

    public void StartDialogue()
    {
        if (isDialogueRunning) return;

        if (steps == null || steps.Length == 0)
        {
            Debug.LogWarning("BossDefeatChoiceDialogue chưa có dialogue steps.");
            return;
        }

        isDialogueRunning = true;
        currentStepIndex = 0;
        darkScore = 0;
        purifiedScore = 0;
        lastNonNeutralBias = BossDialogueRouteBias.None;

        if (pauseGameWhileChoosing)
            Time.timeScale = 0f;

        if (dialoguePanel != null)
            dialoguePanel.SetActive(true);

        if (speakerText != null)
            speakerText.gameObject.SetActive(true);

        if (dialogueText != null)
            dialogueText.gameObject.SetActive(true);

        if (resultPanel != null)
            resultPanel.SetActive(false);

        ShowCurrentStep();
    }

    private void ShowCurrentStep()
    {
        if (currentStepIndex < 0 || currentStepIndex >= steps.Length)
        {
            StartCoroutine(FinishDialogueRoutine());
            return;
        }

        BossDialogueStep step = steps[currentStepIndex];

        if (speakerText != null)
            speakerText.text = step.speakerName;

        if (dialogueText != null)
            dialogueText.text = step.dialogueText;

        int choiceCount = step.choices != null ? step.choices.Length : 0;

        for (int i = 0; i < choiceButtons.Length; i++)
        {
            bool active = i < choiceCount && step.choices[i] != null;

            if (choiceButtons[i] != null)
                choiceButtons[i].gameObject.SetActive(active);

            if (!active) continue;

            TMP_Text buttonText = GetButtonText(i);

            if (buttonText != null)
                buttonText.text = step.choices[i].choiceText;
        }
    }

    private TMP_Text GetButtonText(int index)
    {
        if (choiceButtonTexts != null && index < choiceButtonTexts.Length && choiceButtonTexts[index] != null)
            return choiceButtonTexts[index];

        if (choiceButtons != null && index < choiceButtons.Length && choiceButtons[index] != null)
            return choiceButtons[index].GetComponentInChildren<TMP_Text>();

        return null;
    }

    private void SelectChoice(int index)
    {
        if (!isDialogueRunning) return;
        if (currentStepIndex < 0 || currentStepIndex >= steps.Length) return;

        BossDialogueStep step = steps[currentStepIndex];

        if (step.choices == null) return;
        if (index < 0 || index >= step.choices.Length) return;

        BossDialogueChoice choice = step.choices[index];
        if (choice == null) return;

        darkScore += choice.darkPoint;
        purifiedScore += choice.purifiedPoint;

        if (choice.tieBreakerBias != BossDialogueRouteBias.None)
            lastNonNeutralBias = choice.tieBreakerBias;

        if (debugLog)
        {
            Debug.Log(
                $"Dialogue choice: +Dark {choice.darkPoint}, +Purified {choice.purifiedPoint} | " +
                $"Total Dark={darkScore}, Purified={purifiedScore}"
            );
        }

        currentStepIndex++;

        if (currentStepIndex >= steps.Length)
        {
            StartCoroutine(FinishDialogueRoutine());
        }
        else
        {
            ShowCurrentStep();
        }
    }

    private IEnumerator FinishDialogueRoutine()
    {
        BossDialogueRouteBias result = ResolveResult();

        // Ẩn toàn bộ phần thoại và button trước.
        HideDialogueRuntimeUI();

        // Hiện bảng kết quả nếu bật.
        if (showResultPanel && resultPanel != null)
        {
            resultPanel.SetActive(true);

            if (resultText != null)
            {
                string resultName = result == BossDialogueRouteBias.Dark ? "Dark Form" : "Purified Form";

                resultText.text =
                    $"Dark: {darkScore}\n" +
                    $"Purified: {purifiedScore}\n\n" +
                    $"Result: {resultName}";
            }

            yield return new WaitForSecondsRealtime(resultShowDuration);

            resultPanel.SetActive(false);
        }

        // Mở lại timeScale trước khi biến dạng, vì EvolveRoutine dùng WaitForSeconds.
        if (pauseGameWhileChoosing)
            Time.timeScale = 1f;

        ApplyResult(result);
        FinishAfterChoice();

        // Tắt sạch lần cuối cho chắc.
        HideDialogueRuntimeUI();

        isDialogueRunning = false;
    }

    private void HideDialogueRuntimeUI()
    {
        if (dialoguePanel != null)
            dialoguePanel.SetActive(false);

        if (speakerText != null)
            speakerText.gameObject.SetActive(false);

        if (dialogueText != null)
            dialogueText.gameObject.SetActive(false);

        if (choiceButtons != null)
        {
            for (int i = 0; i < choiceButtons.Length; i++)
            {
                if (choiceButtons[i] != null)
                    choiceButtons[i].gameObject.SetActive(false);
            }
        }
    }

    private BossDialogueRouteBias ResolveResult()
    {
        if (darkScore > purifiedScore) return BossDialogueRouteBias.Dark;
        if (purifiedScore > darkScore) return BossDialogueRouteBias.Purified;

        if (lastNonNeutralBias != BossDialogueRouteBias.None)
            return lastNonNeutralBias;

        // Safe fallback. You can change this to Dark if your story needs it.
        return BossDialogueRouteBias.Purified;
    }

    private void ApplyResult(BossDialogueRouteBias result)
    {
        if (playerMovement == null)
        {
            Debug.LogWarning("BossDefeatChoiceDialogue chưa gán PlayerMovement.");
            return;
        }

        if (result == BossDialogueRouteBias.Dark)
            playerMovement.EvolveToDarkRoute();
        else
            playerMovement.EvolveToPurifiedRoute();
    }

    private void FinishAfterChoice()
    {
        if (hideBossAfterChoice && bossObjectToDisable != null)
            bossObjectToDisable.SetActive(false);

        if (openGateAfterChoice && gateToOpen != null)
            gateToOpen.SetActive(false);
    }
}
