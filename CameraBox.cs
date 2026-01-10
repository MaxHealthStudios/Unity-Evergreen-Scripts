using UnityEngine;

[RequireComponent(typeof(BoxCollider2D))]
public class CameraBox : MonoBehaviour {
    private Camera cam;
    private BoxCollider2D box;
    private float sizeX, sizeY, ratio;

    void Awake() {
        cam = GetComponent<Camera>();
        cam.orthographic = true;
        box = GetComponent<BoxCollider2D>();
        box.isTrigger = true;
    }

    void Update() {
        CalculateCameraBox();
    }

    public void CalculateCameraBox() {
        sizeY = cam.orthographicSize * 2;
        ratio = (float)Screen.width / (float)Screen.height;
        sizeX = ratio * sizeY;
        box.size = new Vector2(sizeX, sizeY);
    }
}