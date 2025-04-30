using UnityEngine;
using UnityEngine.EventSystems;

public class PanningZoom : MonoBehaviour
{
    Vector3 touchStart;

    public float zoomMin, zoomMax;
    public float zoomMultiplier = 3f;
    private float zoom;
    private float smoothTime = 0.25f;
    private float velocity = 0f;

    private bool hasTouched = false;
    public LayerMask blockInputLayer;
    bool checkSurface = false;

    private void Start()
    {
        zoom = Camera.main.orthographicSize;
    }

    private void Update() 
    {
            if (EventSystem.current.IsPointerOverGameObject())
                return;
                
            if (Input.GetMouseButtonDown(0))
            {
                touchStart = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            }

            if (Input.touchCount == 2)
            {
                Touch touchZero = Input.GetTouch(0);
                Touch touchOne = Input.GetTouch(1);

                Vector2 touchZeroPrevPos = touchZero.position - touchZero.deltaPosition;
                Vector2 touchOnePrevPos = touchOne.position - touchOne.deltaPosition;

                float prevMagnitude = (touchZeroPrevPos - touchOnePrevPos).magnitude;
                float currentMagnitude = (touchZero.position - touchOne.position).magnitude;

                float difference = currentMagnitude - prevMagnitude;

                Zoom(difference * 0.01f);
            }
            
            else if (Input.GetMouseButton(0))
            {
                Vector3 direction = touchStart - Camera.main.ScreenToWorldPoint(Input.mousePosition);
                Camera.main.transform.position += direction;
            }

            Zoom(Input.GetAxis("Mouse ScrollWheel"));
    }

    private void Zoom(float increment)
    {
        zoom -= increment * zoomMultiplier;
        zoom = Mathf.Clamp(zoom, zoomMin, zoomMax);
        Camera.main.orthographicSize = Mathf.SmoothDamp(Camera.main.orthographicSize, zoom, ref velocity, smoothTime);
    }

    private bool IsPointerOverLayer()
    {
        if (!Input.GetMouseButton(0)) return false; // Проверяем только если нажата кнопка мыши

        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
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

            /*Touch touch = Input.GetTouch(0);

            checkSurface = EventSystem.current.IsPointerOverGameObject(touch);*/

            /*Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);

            RaycastHit2D hit = Physics2D.Raycast(ray.origin, ray.direction, Mathf.Infinity, layerMask);*/

            checkSurface = EventSystem.current.IsPointerOverGameObject();
            //(hit.collider != null) ? true : false
            //Debug.Log(hit.collider);
        }

        else if (Input.GetMouseButtonUp(0))
        {
            hasTouched = false;
        }

        /*Debug.Log(hasTouched + " Has");*/

        return checkSurface;
    }

    
}   
