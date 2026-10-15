using System.Collections;
using TMPro;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

public class TutorialManager : NetworkBehaviour
{
    public enum TutorialStep
    {
        AimAtGhost,
        PlaceGreenCube,
        PlaceRedCube,
        ClickFakeKey,
        ClickDoorToReset,
        TakeNotebook,
        OpenNotebook,
        Completed
    }


    [SerializeField] private TMP_Text ghostDialogueDisplay;

    [SerializeField] private GameObject greenCube;
    [SerializeField] private GameObject redCube;

    [SerializeField] private GameObject fakeKeyObject;
    [SerializeField] private GameObject realKey;
    [SerializeField] private GameObject ghostHandNotebook;

    public readonly NetworkVariable<TutorialStep> CurrentStep = new(
        TutorialStep.AimAtGhost,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private readonly NetworkVariable<FixedString512Bytes> syncedDialogue = new(
        string.Empty,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private readonly string msgIntro = "<mark=#FFFFFF0F><color=white>Witaj zagubiona duszo. Widzê, ¿e znalaz³eœ/aœ <color=yellow><i>latarkê indukcyjn¹</i></color>. Jest to potê¿ny artefakt który pozwala rozmawiaæ z duchami <color=yellow><i>wielkich kasyniarzy</i></color>. Spróbuj skierowaæ j¹ na mnie i naciœnij spust.</color></mark>";
    private readonly string msgGreenCube = "<mark=#FFFFFF0F><color=white>Wspaniale! Nie by³em pewny czy zdo³asz okie³znaæ tak potê¿ny artefakt. Skoro znasz ju¿ podstawy przenieœ ten <color=green>zielony szeœcian</color> na <color=green>zielon¹ podk³adkê</color>. Wyceluj latark¹ i naciœnij spust, by go podnieœæ/upuœciæ.</color></mark>";
    private readonly string msgRedCube = "<mark=#FFFFFF0F><color=white>Cudownie! Mam dobre przeczucie co do ciebie, teraz przenieœ <color=red>czerwony szeœcian</color> na <color=red>czerwon¹ podk³adkê</color>. By nie musieæ siê gimnastykowaæ mo¿esz manipulowaæ moc¹ latarki u¿ywaj¹c joysticka, by oddalaæ i przybli¿aæ obiekt!</color></mark>";
    private readonly string msgFakeKey = "<mark=#FFFFFF0F><color=white>Niesamowite! Z komody przy drzwiach wy³oni³ siê klucz. Podnieœ go i w³ó¿ do drzwi by siê z t¹d wydostaæ!</color></mark>";
    private readonly string msgDoorReset = "<mark=#FFFFFF0F><color=white>Cholibka, obiekty w tym miejscu potrafi¹ znikaæ! Naszczêœcie zwykle s¹ powi¹zane z innymi i wystarczy ¿e go klikniesz by zresetowaæ ich stan. Spróbuj drzwi wyjœciowe.</color></mark>";
    private readonly string msgTakeNotebook = "<mark=#FFFFFF0F><color=white>Matematycznie! Nie mogê jednak opuœciæ z tob¹ tego pokoju by pomóc ci w dalszej wyprawie, ale weŸ ten <color=yellow><i>notatnik wiedzy</i></color>. Z czasem zbierze on kluczowe wskazówki które mog¹ okazaæ siê niezbêdne do opuszczenia tego miejsca.</color></mark>";
    private readonly string msgOpenNotebook = "<mark=#FFFFFF0F><color=white>Kapitalnie! Teraz twoja latarka mo¿e go zmaterializowaæ w ka¿dej chwili. Kliknij przycisk 2 na kontrolerze, by go otworzyæ.</color></mark>";
    private readonly string msgTakeRealKey = "<mark=#FFFFFF0F><color=green>Rewelacyjnie! Wiêcej ciê ju¿ nie nauczê. Bierz klucz i powodzenia!</color></mark>";

    public override void OnNetworkSpawn()
    {
        syncedDialogue.OnValueChanged += OnDialogueChanged;
        CurrentStep.OnValueChanged += OnStepChanged;

        if (ghostDialogueDisplay != null && !syncedDialogue.Value.IsEmpty)
            ghostDialogueDisplay.text = syncedDialogue.Value.ToString();

        ApplyStepEnvironment(CurrentStep.Value);

        if (IsServer && CurrentStep.Value == TutorialStep.AimAtGhost)
        {
            syncedDialogue.Value = msgIntro;
        }
    }

    public override void OnNetworkDespawn()
    {
        syncedDialogue.OnValueChanged -= OnDialogueChanged;
        CurrentStep.OnValueChanged -= OnStepChanged;
    }

    private void OnDialogueChanged(FixedString512Bytes oldVal, FixedString512Bytes newVal)
    {
        if (ghostDialogueDisplay != null) ghostDialogueDisplay.text = newVal.ToString();
    }

    private void OnStepChanged(TutorialStep oldStep, TutorialStep newStep)
    {
        ApplyStepEnvironment(newStep);
    }

    private void ApplyStepEnvironment(TutorialStep step)
    {
        SetItemActive(greenCube, step >= TutorialStep.PlaceGreenCube);
        SetItemActive(redCube, step >= TutorialStep.PlaceRedCube);

        if (fakeKeyObject != null) SetItemActive(fakeKeyObject, step == TutorialStep.ClickFakeKey);

        if (ghostHandNotebook != null) SetItemActive(ghostHandNotebook, step == TutorialStep.TakeNotebook);

        if (realKey != null)
        {
            bool showKey = step >= TutorialStep.TakeNotebook;
            SetItemActive(realKey, showKey);
            if (step < TutorialStep.Completed)
            {
                if (realKey.TryGetComponent<GrabbableObject>(out var grabbable))
                {
                    grabbable.enabled = false;
                }
            }
        }
    }

    public void OnGhostClicked()
    {
        if (!IsServer || CurrentStep.Value != TutorialStep.AimAtGhost) return;

        CurrentStep.Value = TutorialStep.PlaceGreenCube;
        syncedDialogue.Value = msgGreenCube;
    }

    public void OnGreenPadPlaced()
    {
        if (!IsServer || CurrentStep.Value != TutorialStep.PlaceGreenCube) return;

        CurrentStep.Value = TutorialStep.PlaceRedCube;
        syncedDialogue.Value = msgRedCube;
    }

    public void OnRedPadPlaced()
    {
        if (!IsServer || CurrentStep.Value != TutorialStep.PlaceRedCube) return;

        CurrentStep.Value = TutorialStep.ClickFakeKey;
        syncedDialogue.Value = msgFakeKey;
    }

    public void OnFakeKeyClicked()
    {
        if (!IsServer || CurrentStep.Value != TutorialStep.ClickFakeKey) return;

        if (fakeKeyObject != null) fakeKeyObject.SetActive(false);

        CurrentStep.Value = TutorialStep.ClickDoorToReset;
        syncedDialogue.Value = msgDoorReset;
    }

    public void OnDoorClickedForReset()
    {
        Debug.Log("[Roulette] B£¥D! Z³e pole.");
        if (!IsServer || CurrentStep.Value != TutorialStep.ClickDoorToReset) return;

        CurrentStep.Value = TutorialStep.TakeNotebook;
        syncedDialogue.Value = msgTakeNotebook;
    }

    public void OnNotebookTaken()
    {
        if (!IsServer || CurrentStep.Value != TutorialStep.TakeNotebook) return;

        if (ghostHandNotebook != null) ghostHandNotebook.SetActive(false);

        //CurrentStep.Value = TutorialStep.OpenNotebook;
        //syncedDialogue.Value = msgOpenNotebook;

        CurrentStep.Value = TutorialStep.Completed;
        syncedDialogue.Value = msgTakeRealKey;
    }

    public void OnNotebookOpenedByPlayer()
    {
        if (!IsServer || CurrentStep.Value != TutorialStep.OpenNotebook) return;

        CurrentStep.Value = TutorialStep.Completed;
        syncedDialogue.Value = msgTakeRealKey;
    }

    private void SetItemActive(GameObject item, bool active)
    {
        if (item == null) return;
        var renderers = item.GetComponentsInChildren<Renderer>(true);
        foreach (var r in renderers) r.enabled = active;

        var colliders = item.GetComponentsInChildren<Collider>(true);
        foreach (var c in colliders) c.enabled = active;

        if (item.TryGetComponent<GrabbableObject>(out var grabbable))
        {
            grabbable.enabled = active;
        }

        if (IsServer && item.TryGetComponent<Rigidbody>(out var rb))
        {
            rb.isKinematic = !active;
            rb.useGravity = active;
        }
    }
}