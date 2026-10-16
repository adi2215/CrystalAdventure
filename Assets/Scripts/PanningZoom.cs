using UnityEngine;
using UnityEngine.EventSystems;

public class PanningZoom : MonoBehaviour
{
    private Vector3 touchStart;

    public float zoomMin, zoomMax;
    public float zoomMultiplier = 3f;
    private float zoom;
    private float smoothTime = 0.25f;
    private float velocity = 0f;

    public LayerMask blockInputLayer;

    private bool startedOverUI = false;

    private Camera cam;

    private void Start()
    {
        cam = Camera.main;
        if (cam != null) zoom = cam.orthographicSize;
    }

    static bool PointerOverUI(int pointerId = -1) =>
        EventSystem.current != null && EventSystem.current.IsPointerOverGameObject(pointerId);

   private void Update()
    {
        if (cam == null) { cam = Camera.main; if (cam == null) return; }
        #if UNITY_EDITOR || UNITY_STANDALONE
                HandleMouse();
        #else
                HandleTouch();
        #endif
    }

    private void HandleMouse()
    {
        if (PointerOverUI())
        {
            if (!startedOverUI)
                startedOverUI = PointerOverUI();

            return;
        }

        if (Input.GetMouseButtonDown(0))
        {
            startedOverUI = PointerOverUI();
            touchStart = cam.ScreenToWorldPoint(Input.mousePosition);
        }

        if (Input.GetMouseButtonUp(0))
        {
            startedOverUI = false;
        }

        if (Input.GetMouseButton(0) && !startedOverUI)
        {
            Vector3 direction = touchStart - cam.ScreenToWorldPoint(Input.mousePosition);
            cam.transform.position += direction;
        }

        Zoom(Input.GetAxis("Mouse ScrollWheel"));
    }

    private void HandleTouch()
    {
        if (Input.touchCount == 1)
        {
            Touch touch = Input.GetTouch(0);

            if (touch.phase == TouchPhase.Began)
            {
                startedOverUI = PointerOverUI(touch.fingerId);
                touchStart = cam.ScreenToWorldPoint(touch.position);
            }

            if (touch.phase == TouchPhase.Moved && !startedOverUI)
            {
                Vector3 direction = touchStart - cam.ScreenToWorldPoint(touch.position);
                cam.transform.position += direction;
            }

            if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled)
            {
                startedOverUI = false;
            }
        }

        else if (Input.touchCount == 2)
        {
            Touch touchZero = Input.GetTouch(0);
            Touch touchOne = Input.GetTouch(1);

            Vector2 touchZeroPrev = touchZero.position - touchZero.deltaPosition;
            Vector2 touchOnePrev = touchOne.position - touchOne.deltaPosition;

            float prevMag = (touchZeroPrev - touchOnePrev).magnitude;
            float currentMag = (touchZero.position - touchOne.position).magnitude;

            float difference = currentMag - prevMag;

            Zoom(difference * 0.01f);
        }
    }

    private void Zoom(float increment)
    {
        zoom -= increment * zoomMultiplier;
        zoom = Mathf.Clamp(zoom, zoomMin, zoomMax);
        cam.orthographicSize = Mathf.SmoothDamp(cam.orthographicSize, zoom, ref velocity, smoothTime);
    }

    /*private bool IsPointerOverLayer()
    {
        if (!Input.GetMouseButton(0)) return false; // Проверяем только если нажата кнопка мыши

        Ray ray = cam.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, blockInputLayer))
        {
            Debug.Log("Клик по объекту: " + hit.collider.gameObject.name); // Проверяем, сработал ли Raycast
            return true; // Если луч попал в объект слоя, блокируем ввод
        }

        return false;
    }


    private bool CheckPoint()
    {
        if ((Input.touchCount > 0 || Input.GetMouseButton(0)) && !hasTouched)
        {
            hasTouched = true;

            Touch touch = Input.GetTouch(0);

            checkSurface = EventSystem.current.IsPointerOverGameObject(touch);

            Ray ray = cam.ScreenPointToRay(Input.mousePosition);

            RaycastHit2D hit = Physics2D.Raycast(ray.origin, ray.direction, Mathf.Infinity, layerMask);

            checkSurface = PointerOverUI();
            //(hit.collider != null) ? true : false
            //Debug.Log(hit.collider);
        }

        else if (Input.GetMouseButtonUp(0))
        {
            hasTouched = false;
        }

        Debug.Log(hasTouched + " Has");

        return checkSurface;
    }*/
}   
