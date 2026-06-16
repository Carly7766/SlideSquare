using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class SlideButton : MonoBehaviour
{
    public int  Row;
    public bool SlideRight;

    private void Start()
    {
        GetComponent<Button>().onClick.AddListener(
            () => GameManager.Instance.OnSlideClicked(Row, SlideRight));
    }
}
