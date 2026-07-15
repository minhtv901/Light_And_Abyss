using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CombatPhaseSequenceManager : MonoBehaviour
{
    [Header("Phases In Order")]
    public List<CombatZoneSpawner> phases = new List<CombatZoneSpawner>();

    [Header("Start")]
    public bool startFirstPhaseOnPlay = true;
    public float delayBeforeFirstPhase = 0f;
    public float delayBeforeNextPhase = 1f;

    [Header("Report")]
    public bool resetGlobalReportOnStart = true;
    public bool printFinalReportWhenFinished = true;

    [Header("Debug")]
    public bool debugLog = true;

    private int currentPhaseIndex = -1;
    private bool sequenceStarted = false;
    private bool sequenceFinished = false;

    private void Start()
    {
        RegisterPhaseEvents();

        if (resetGlobalReportOnStart)
        {
            CombatZoneSpawner.ResetGlobalSpawnReport();
        }

        if (startFirstPhaseOnPlay)
        {
            StartSequence();
        }
    }

    public void StartSequence()
    {
        if (sequenceStarted) return;

        sequenceStarted = true;
        sequenceFinished = false;

        StartCoroutine(StartFirstPhaseRoutine());
    }

    private IEnumerator StartFirstPhaseRoutine()
    {
        if (delayBeforeFirstPhase > 0f)
        {
            yield return new WaitForSeconds(delayBeforeFirstPhase);
        }

        StartPhase(0);
    }

    private void RegisterPhaseEvents()
    {
        for (int i = 0; i < phases.Count; i++)
        {
            if (phases[i] == null) continue;

            // Manager điều khiển thứ tự phase, nên từng spawner không tự chạy khi Player đi vào trigger.
            phases[i].autoStartOnTrigger = false;

            phases[i].OnPhaseCleared -= HandlePhaseCleared;
            phases[i].OnPhaseCleared += HandlePhaseCleared;
        }
    }

    private void StartPhase(int index)
    {
        if (sequenceFinished) return;

        if (index < 0 || index >= phases.Count)
        {
            FinishSequence();
            return;
        }

        currentPhaseIndex = index;

        CombatZoneSpawner phase = phases[index];

        if (phase == null)
        {
            StartCoroutine(StartNextPhaseAfterDelay());
            return;
        }

        if (debugLog)
        {
            Debug.Log($"START PHASE {phase.phaseIndex}");
        }

        phase.StartSpawner();
    }

    private void HandlePhaseCleared(CombatZoneSpawner clearedPhase)
    {
        if (sequenceFinished) return;
        if (clearedPhase == null) return;

        if (currentPhaseIndex < 0 ||
            currentPhaseIndex >= phases.Count ||
            phases[currentPhaseIndex] != clearedPhase)
        {
            if (debugLog)
            {
                Debug.LogWarning(
                    $"Nhận clear từ phase không phải phase hiện tại: {clearedPhase.phaseIndex}. Bỏ qua."
                );
            }

            return;
        }

        if (debugLog)
        {
            Debug.Log($"CLEAR PHASE {clearedPhase.phaseIndex}");
        }

        StartCoroutine(StartNextPhaseAfterDelay());
    }

    private IEnumerator StartNextPhaseAfterDelay()
    {
        if (delayBeforeNextPhase > 0f)
        {
            yield return new WaitForSeconds(delayBeforeNextPhase);
        }

        int nextIndex = currentPhaseIndex + 1;

        if (nextIndex >= phases.Count)
        {
            FinishSequence();
        }
        else
        {
            StartPhase(nextIndex);
        }
    }

    private void FinishSequence()
    {
        if (sequenceFinished) return;

        sequenceFinished = true;

        if (debugLog)
        {
            Debug.Log("TẤT CẢ PHASE ĐÃ CLEAR.");
        }

        if (printFinalReportWhenFinished)
        {
            CombatZoneSpawner.PrintGlobalSpawnReport();
        }
    }

    public void ResetSequence()
    {
        StopAllCoroutines();

        for (int i = 0; i < phases.Count; i++)
        {
            if (phases[i] == null) continue;
            phases[i].ResetSpawner();
        }

        if (resetGlobalReportOnStart)
        {
            CombatZoneSpawner.ResetGlobalSpawnReport();
        }

        currentPhaseIndex = -1;
        sequenceStarted = false;
        sequenceFinished = false;
    }

    private void OnDestroy()
    {
        for (int i = 0; i < phases.Count; i++)
        {
            if (phases[i] == null) continue;
            phases[i].OnPhaseCleared -= HandlePhaseCleared;
        }
    }
}
