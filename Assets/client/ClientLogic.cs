using UnityEngine;
using UnityEngine.UI;

public class ClientLogic : MonoBehaviour
{
    public Connection connection;  

    private bool isWebSocketConnected = false;

    public RawImage colorImage;
    public RawImage depthImage;

    void Start()
    {
        StartWebSocket();
    }

    void Update()
    {
        if (colorImage != null && depthImage != null && isWebSocketConnected)
        {
            Texture2D colorTexture = ConvertToTexture2D(colorImage.texture);
            Texture2D depthTexture = ConvertToTexture2D(depthImage.texture);

            if (colorTexture != null)
            {
                byte[] colorImageBytes = colorTexture.EncodeToJPG();
                connection.SendText("color");
                connection.SendWebSocketMessage(colorImageBytes);
            }

            if(depthTexture != null)
            {
                byte[] depthImageBytes = depthTexture.EncodeToJPG();
                connection.SendText("depth");
                connection.SendWebSocketMessage(depthImageBytes);
            }
            connection.SendText("position");
            connection.SendText("rotation");
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
            // If it's already a Texture2D, return it
            return tex2D;
        }
        else if (texture is RenderTexture renderTex)
        {
            // If it's a RenderTexture, convert it to Texture2D
            RenderTexture currentRT = RenderTexture.active;
            RenderTexture.active = renderTex;

            Texture2D newTexture = new Texture2D(renderTex.width, renderTex.height, TextureFormat.RGBA32, false);
            newTexture.ReadPixels(new Rect(0, 0, renderTex.width, renderTex.height), 0, 0);
            newTexture.Apply();

            RenderTexture.active = currentRT; // Restore the previously active RenderTexture
            return newTexture;
        }
        return null; // Return null if it's an unsupported texture type
    }
}
