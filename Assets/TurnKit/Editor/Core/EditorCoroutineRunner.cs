using System.Collections;
using UnityEditor;

namespace TurnKit.Editor
{
    public static class EditorCoroutineRunner
    {
        public static void StartCoroutine(IEnumerator coroutine)
        {
            EditorApplication.update += () => UpdateCoroutine(coroutine);
        }
        
        static void UpdateCoroutine(IEnumerator coroutine)
        {
            if (coroutine == null || !coroutine.MoveNext())
            {
                EditorApplication.update -= () => UpdateCoroutine(coroutine);
            }
        }
    }
}