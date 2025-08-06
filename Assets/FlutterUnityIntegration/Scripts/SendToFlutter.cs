using UnityEngine;

public static class SendToFlutter
{
      /// <summary>
      /// Отправляет сообщение во Flutter через flutter_embed_unity
      /// </summary>
      /// <param name="message">Сообщение для отправки</param>
      public static void Send(string message)
      {
            if (string.IsNullOrEmpty(message))
            {
                  Debug.LogWarning("⚠️ Попытка отправить пустое сообщение во Flutter");
                  return;
            }

            try
            {
#if UNITY_IOS && !UNITY_EDITOR
            // Для iOS используем native plugin
            _sendMessageToFlutter(message);
#elif UNITY_ANDROID && !UNITY_EDITOR
            // Для Android используем Java
            using (AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            {
                AndroidJavaObject activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
                activity.Call("sendToFlutter", message);
            }
#else
                  // В редакторе просто логируем
                  Debug.Log($"[EDITOR] SendToFlutter: {message}");
#endif
            }
            catch (System.Exception e)
            {
                  Debug.LogError($"❌ Ошибка отправки сообщения во Flutter: {e.Message}");
            }
      }

#if UNITY_IOS && !UNITY_EDITOR
    [System.Runtime.InteropServices.DllImport("__Internal")]
    private static extern void _sendMessageToFlutter(string message);
#endif
}
