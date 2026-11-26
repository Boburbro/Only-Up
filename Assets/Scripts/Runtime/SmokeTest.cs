#if DEVELOPMENT_BUILD
using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace OnlyUp
{
    /// <summary>
    /// Faqat sinov (development) buildida: "-smoke" bilan ishga tushirilsa, menyu va xaritani
    /// ochib FPS'ni logga yozadi va chiqadi. Saqlash va rekordlarga yozmaydi.
    /// </summary>
    class SmokeTest : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod]
        static void Run()
        {
            if (!System.Environment.GetCommandLineArgs().Contains("-smoke")) return;
            var host = new GameObject("SmokeTest").AddComponent<SmokeTest>();
            DontDestroyOnLoad(host.gameObject);
            host.StartCoroutine(host.Check());
        }

        IEnumerator Check()
        {
            Debug.Log($"SMOKE gpu={SystemInfo.graphicsDeviceName} screen={Screen.width}x{Screen.height}");
            yield return Measure("menu");
            yield return SceneManager.LoadSceneAsync(GameScenes.Game);
            // O'yinchi qimirlamaydi va vaqt to'xtatiladi, shunda hech narsa saqlanmaydi.
            var session = FindAnyObjectByType<GameSession>();
            yield return null;
            session.enabled = false;
            yield return Measure("world-start");
            var motor = FindAnyObjectByType<PlayerMotor>();
            var route = FindAnyObjectByType<CourseRoute>();
            foreach (int i in new[] { 42, 100, 150 })
            {
                var bounds = route.steps[i].bounds;
                motor.Teleport(new Vector3(bounds.center.x, bounds.max.y + 0.05f, bounds.center.z));
                yield return Measure("world-step" + i);
            }
            Debug.Log("SMOKE done");
            Application.Quit();
        }

        static IEnumerator Measure(string label)
        {
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = -1;
            yield return new WaitForSecondsRealtime(1.5f);
            int frames = 0;
            float start = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - start < 3f)
            {
                yield return null;
                frames++;
            }
            Debug.Log($"SMOKE {label} fps={frames / (Time.realtimeSinceStartup - start):0}");
        }
    }
}
#endif
