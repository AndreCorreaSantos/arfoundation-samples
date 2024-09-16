using UnityEngine;
using UnityEngine.UI;
using System.Threading.Tasks;
using System.Text;
using System.Collections.Generic;

public class ClientLogic : MonoBehaviour
{
    public Connection connection;
    private bool isWebSocketConnected = false;

    public RawImage colorImage;
    public RawImage depthImage;
    public Transform playerTransform;
    public GameObject UICanvas;
    public GameObject anchorPrefab;   // Assign your anchor prefab in the Inspector

    private byte[] colorImageBytes;
    private byte[] depthImageBytes;

    private float timeSinceLastSend = 0f;
    private float sendInterval = 0.5f; // Adjust as needed

    private List<GameObject> anchors = new List<GameObject>();

    void Start()
    {
        StartWebSocket();
        SpawnUI();

        // Subscribe to server messages
        connection.OnServerMessage += HandleServerMessage;
    }

    void Update()
    {
        timeSinceLastSend += Time.deltaTime;

        if (timeSinceLastSend >= sendInterval && colorImage != null && depthImage != null && isWebSocketConnected)
        {
            timeSinceLastSend = 0f;

            // Perform image encoding on the main thread
            Texture2D colorTexture = ConvertToTexture2D(colorImage.texture);
            Texture2D depthTexture = ConvertToTexture2D(depthImage.texture);

            if (colorTexture != null)
            {
                colorImageBytes = colorTexture.EncodeToJPG();
            }

            if (depthTexture != null)
            {
                depthImageBytes = depthTexture.EncodeToJPG();
            }

            // Send data asynchronously
            SendDataAsync();
        }

        // Remove destroyed anchors from the list
        anchors.RemoveAll(anchor => anchor == null);
    }

    // Handle incoming messages from the server
    private void HandleServerMessage(string message)
    {
        Debug.Log("Received from server: " + message);

        if (message.StartsWith("object_position"))
        {
            string data = message.Replace("object_position", "").Trim();
            string[] parts = data.Split(' ');

            if (parts.Length == 3 &&
                float.TryParse(parts[0], out float x) &&
                float.TryParse(parts[1], out float y) &&
                float.TryParse(parts[2], out float z))
            {
                Vector3 objectPosition = new Vector3(x, y, z);

                // Spawn the anchor at the received position
                SpawnAnchor(objectPosition);
            }
            else
            {
                Debug.LogWarning("Invalid object position data received.");
            }
        }
    }

    // Spawn the anchor in the scene
    private void SpawnAnchor(Vector3 position)
    {
        if (anchorPrefab != null)
        {
            // Check for existing anchors within a 1.0 unit radius
            bool anchorNearby = false;

            foreach (GameObject anchor in anchors)
            {
                if (anchor != null)
                {
                    float distance = Vector3.Distance(position, anchor.transform.position);
                    if (distance <= 1.0f)
                    {
                        anchorNearby = true;
                        break;
                    }
                }
            }

            if (!anchorNearby)
            {
                // Instantiate the anchor
                GameObject newAnchor = Instantiate(anchorPrefab, position, Quaternion.identity);
                newAnchor.layer = 30; // COMMENT THIS IF NOT DEBUGGING

                // Set the playerTransform on the Anchor script
                Anchor anchorScript = newAnchor.GetComponent<Anchor>();
                if (anchorScript != null)
                {
                    anchorScript.playerTransform = playerTransform;
                }
                else
                {
                    Debug.LogWarning("Anchor component not found on the instantiated prefab.");
                }

                // Add the new anchor to the list
                anchors.Add(newAnchor);
            }
            else
            {
                Debug.Log("An anchor already exists within 1.0 units. Not spawning a new one.");
            }
        }
        else
        {
            Debug.LogWarning("anchorPrefab is not assigned in the Inspector.");
        }
    }

    // Async method to handle sending data
    private async void SendDataAsync()
    {
        if (colorImageBytes != null)
        {
            await SendImageDataAsync("color", colorImageBytes);
        }

        if (depthImageBytes != null)
        {
            await SendImageDataAsync("depth", depthImageBytes);
        }
    }

    private async Task SendImageDataAsync(string imageType, byte[] imageBytes)
    {
        // Get the player's position and rotation at the time of sending
        Vector3 pos = playerTransform.position;
        Quaternion rot = playerTransform.rotation;

        // Create a data object to serialize
        var dataObject = new
        {
            type = imageType,
            position = new { x = pos.x, y = pos.y, z = pos.z },
            rotation = new { x = rot.x, y = rot.y, z = rot.z, w = rot.w },
            imageData = Convert.ToBase64String(imageBytes)
        };

        // Serialize the object to JSON
        string jsonString = JsonSerializer.Serialize(dataObject);

        // Send the JSON string
        await connection.SendTextAsync(jsonString);
    }

    // Spawn UI in front of the player
    private void SpawnUI()
    {
        GameObject ui = Instantiate(UICanvas, playerTransform.position + playerTransform.forward * 2, playerTransform.rotation);
        ui.transform.LookAt(playerTransform);

        SetLayerRecursively(ui, 30);
    }

    // Recursive method to set the layer for a GameObject and all its children
    private void SetLayerRecursively(GameObject obj, int layer)
    {
        obj.layer = layer;
        foreach (Transform child in obj.transform)
        {
            SetLayerRecursively(child.gameObject, layer);
        }
    }

    public void StartWebSocket()
    {
        connection.StartConnection();
        isWebSocketConnected = true;
    }

    private Texture2D ConvertToTexture2D(Texture texture)
    {
        if (texture is Texture2D tex2D)
        {
            return tex2D;
        }
        else if (texture is RenderTexture renderTex)
        {
            RenderTexture currentRT = RenderTexture.active;
            RenderTexture.active = renderTex;

            Texture2D newTexture = new Texture2D(renderTex.width, renderTex.height, TextureFormat.RGBA32, false);
            newTexture.ReadPixels(new Rect(0, 0, renderTex.width, renderTex.height), 0, 0);
            newTexture.Apply();

            RenderTexture.active = currentRT;
            return newTexture;
        }
        return null;
    }
}
