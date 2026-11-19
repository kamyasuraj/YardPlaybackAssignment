using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace Lab.EditorTools
{
    /// <summary>Menu Lab › View 1–4 (Alt+1–4): shows that fixed view in the Game view, in Edit or Play mode.</summary>
    public static class LabViewMenu
    {
        [MenuItem("Lab/View 1 &1")] static void View1() => Show(0);
        [MenuItem("Lab/View 2 &2")] static void View2() => Show(1);
        [MenuItem("Lab/View 3 &3")] static void View3() => Show(2);
        [MenuItem("Lab/View 4 &4")] static void View4() => Show(3);

        static void Show(int index)
        {
            var switcher = Object.FindAnyObjectByType<ViewSwitcher>();
            if (switcher == null)
            {
                Debug.LogWarning("[Lab] Open Assets/Scenes/Lab_Starter.unity: it has the View_1..View_4 cameras.");
                return;
            }
            switcher.Show(index);
            InternalEditorUtility.RepaintAllViews();
        }
    }
}
