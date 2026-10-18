using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Cheat card data - contains an ID and a prefab
/// </summary>
[System.Serializable]
public class CheatCard
{
    public int cheatId;
    public GameObject cardPrefab;
}

public class CardGameController : MonoBehaviour
{
    [Header("Card Prefabs")]
    public GameObject startCardPrefab;
    public List<GameObject> anaCardPrefabs = new List<GameObject>();
    public GameObject endCardPrefab;

    [Header("Special Cards")]
    public GameObject optionsCardPrefab; // Options (pause) card - single prefab
    public List<CheatCard> cheatCardPrefabs = new List<CheatCard>(); // Cheat cards - numbered list

    [Header("Settings")]
    public RectTransform spawnPoint;

    private Stack<GameObject> cardStack = new Stack<GameObject>();
    private int currentAnaCardIndex = 0;
    private bool gameStarted = false;

    void Start()
    {
        if (spawnPoint == null)
        {
            Debug.LogError("Spawn Point is not assigned!");
            return;
        }
        // Start background music
        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayBackgroundMusic();

        // Show the start card
        if (startCardPrefab != null)
        {
            SpawnCardOfType(startCardPrefab, CardType.StartGame);
        }
        else
        {
            Debug.LogWarning("Start card prefab is not assigned!");
        }
    }

    private void SpawnCardOfType(GameObject prefab, CardType cardType)
    {
        if (prefab == null) return;

        GameObject cardObj = Instantiate(prefab, spawnPoint, false);
        RectTransform rectTransform = cardObj.GetComponent<RectTransform>();
        
        if (rectTransform != null)
        {
            rectTransform.anchoredPosition = Vector2.zero;
            rectTransform.localRotation = Quaternion.identity;
        }

        DragCard dragCard = cardObj.GetComponent<DragCard>();
        if (dragCard != null)
        {
            dragCard.cardType = cardType;
            // Clear existing listeners
            dragCard.onYes.RemoveAllListeners();
            dragCard.onNo.RemoveAllListeners();
            // Add the new listeners
            dragCard.onYes.AddListener(() => OnCardSwiped(cardType, true));
            dragCard.onNo.AddListener(() => OnCardSwiped(cardType, false));
        }

        cardStack.Push(cardObj);
    }

    private void OnCardSwiped(CardType cardType, bool isYes)
    {
        Debug.Log($"Card swiped: {cardType} - Direction: {(isYes ? "RIGHT (Yes)" : "LEFT (No)")}");

        switch (cardType)
        {
            case CardType.StartGame:
                // The start card should only be triggered once
                if (!gameStarted && isYes)
                {
                    gameStarted = true;
                    
                    Debug.Log("Play card accepted - UI updated");
                    ShowNextAnaCard();
                }
                break;

            case CardType.AnaCard:
                ShowNextAnaCard();
                break;

            case CardType.Pause:
                Debug.Log("Pause card swiped!");
                break;

            case CardType.Cheat:
                Debug.Log("Cheat card used!");
                break;

            case CardType.EndGame:
                Debug.Log("Game over!");
                break;

            default:
                break;
        }
    }

    private void ShowNextAnaCard()
    {
        if (currentAnaCardIndex < anaCardPrefabs.Count)
        {
            GameObject cardPrefab = anaCardPrefabs[currentAnaCardIndex];
            SpawnCardOfType(cardPrefab, CardType.AnaCard);
            currentAnaCardIndex++;
        }
        else
        {
            // All cards have been shown
            if (endCardPrefab != null)
            {
                SpawnCardOfType(endCardPrefab, CardType.EndGame);
            }
            else
            {
                Debug.Log("Game over! The end card prefab is not assigned.");
            }
        }
    }

    // Spawn the options card
    public void SpawnOptionsCard()
    {
        Debug.Log("SpawnOptionsCard called");
        if (optionsCardPrefab != null)
        {
            SpawnCardOfType(optionsCardPrefab, CardType.Pause);
        }
        else
        {
            Debug.LogWarning("SpawnOptionsCard: Options card prefab is not assigned!");
        }
    }

    // Spawn a cheat card selected by ID
    public void SpawnCheatCard(int cheatId)
    {
        Debug.Log($"SpawnCheatCard called with ID: {cheatId}");
        
        if (cheatCardPrefabs == null)
        {
            Debug.LogWarning("SpawnCheatCard: Cheat card list is not assigned!");
            return;
        }

        CheatCard selectedCheat = cheatCardPrefabs.Find(c => c != null && c.cheatId == cheatId);
        
        if (selectedCheat != null && selectedCheat.cardPrefab != null)
        {
            SpawnCardOfType(selectedCheat.cardPrefab, CardType.Cheat);
            Debug.Log($"Cheat card #{cheatId} spawned");
        }
        else
        {
            Debug.LogWarning($"SpawnCheatCard: Cheat card with ID #{cheatId} was not found!");
        }
    }


}
