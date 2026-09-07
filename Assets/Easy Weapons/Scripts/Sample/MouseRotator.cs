using UnityEngine;

public class MouseRotator : MonoBehaviour {
	
	// A mouselook behaviour with constraints which operate relative to
	// this gameobject's initial rotation.
	
	// Only rotates around local X and Y.
	
	// Works in local coordinates, so if this object is parented
	// to another moving gameobject, its local constraints will
	// operate correctly
	// (Think: looking out the side window of a car, or a gun turret
	// on a moving spaceship with a limited angular range)
	
	// to have no constraints on an axis, set the rotationRange to 360 or greater.

	public Vector2 rotationRange = new Vector3(70,70); 
	public float rotationSpeed = 10;
	public float dampingTime = 0.2f;
	public bool autoZeroVerticalOnMobile = true;
	public bool autoZeroHorizontalOnMobile = false;
	public bool relative = true;
	Vector3 targetAngles;
	Vector3 followAngles;
	Vector3 followVelocity;
	Quaternion originalRotation;
	
	
	// Use this for initialization
	void Start () {
		originalRotation = transform.localRotation;
	}
	
	// Update is called once per frame
	void Update () {
		
		// we make initial calculations from the original local rotation
		transform.localRotation = originalRotation;
	
		// read input from mouse or mobile controls
		float inputH = 0;
		float inputV = 0;
		if (relative)
		{
			
			inputH = Input.GetAxis("Mouse X");
			inputV = Input.GetAxis("Mouse Y");
			
			// wrap values to avoid springing quickly the wrong way from positive to negative
			if (targetAngles.y > 180) { targetAngles.y -= 360; followAngles.y -= 360; }
			if (targetAngles.x > 180) { targetAngles.x -= 360; followAngles.x-= 360; }
			if (targetAngles.y < -180) { targetAngles.y += 360; followAngles.y += 360; }
			if (targetAngles.x < -180) { targetAngles.x += 360; followAngles.x += 360; }
	
			// with mouse input, we have direct control with no springback required.
			targetAngles.y += inputH * rotationSpeed;
			targetAngles.x += inputV * rotationSpeed;
	
			// clamp values to allowed range
			targetAngles.y = Mathf.Clamp ( targetAngles.y, -rotationRange.y * 0.5f, rotationRange.y * 0.5f );
			targetAngles.x = Mathf.Clamp ( targetAngles.x, -rotationRange.x * 0.5f, rotationRange.x * 0.5f );
	
		} else {
	
			inputH = Input.mousePosition.x;
			inputV = Input.mousePosition.y;
	
			// set values to allowed range
			targetAngles.y = Mathf.Lerp ( -rotationRange.y * 0.5f, rotationRange.y * 0.5f, inputH/Screen.width );
			targetAngles.x = Mathf.Lerp ( -rotationRange.x * 0.5f, rotationRange.x * 0.5f, inputV/Screen.height );
	
	
	
		}
	
	
	
	
	
		// smoothly interpolate current values to target angles
		followAngles = Vector3.SmoothDamp( followAngles, targetAngles, ref followVelocity, dampingTime );
	
		// update the actual gameobject's rotation
		transform.localRotation = originalRotation * Quaternion.Euler( -followAngles.x, followAngles.y, 0 );
		
	}
	   //
    // [Header("Mouse Settings")]
    // [SerializeField] private float sensitivity = 3f;
    // [SerializeField] private float smoothTime = 0.05f;
    //
    // [Header("Vertical Limits")]
    // [SerializeField] private float minVerticalAngle = -89f;
    // [SerializeField] private float maxVerticalAngle = 89f;
    //
    // private float verticalRotation = 0f;
    //
    // private Vector2 currentMouseDelta;
    // private Vector2 mouseDeltaVelocity;
    //
    // private void Start()
    // {
    //     // Запоминаем текущее положение камеры по вертикали
    //     verticalRotation = NormalizeAngle(transform.localEulerAngles.x);
    //
    //     LockCursor();
    // }
    //
    // private void Update()
    // {
    //     // ESC — освободить мышь
    //     if (Input.GetKeyDown(KeyCode.Escape))
    //     {
    //         UnlockCursor();
    //     }
    //
    //     // Клик — снова захватить мышь
    //     if (Input.GetMouseButtonDown(0) && Cursor.lockState != CursorLockMode.Locked)
    //     {
    //         LockCursor();
    //     }
    //
    //     // Не вращаем камеру, если мышь не захвачена
    //     if (Cursor.lockState != CursorLockMode.Locked)
    //         return;
    //
    //     // Получаем движение мыши
    //     float mouseX = Input.GetAxis("Mouse X");
    //     float mouseY = Input.GetAxis("Mouse Y");
    //
    //     Vector2 targetMouseDelta = new Vector2(mouseX, mouseY) * sensitivity;
    //
    //     // Сглаживание движения
    //     currentMouseDelta = Vector2.SmoothDamp(
    //         currentMouseDelta,
    //         targetMouseDelta,
    //         ref mouseDeltaVelocity,
    //         smoothTime
    //     );
    //
    //     // Горизонтальный поворот
    //     transform.Rotate(
    //         Vector3.up,
    //         currentMouseDelta.x,
    //         Space.Self
    //     );
    //
    //     // Вертикальный поворот
    //     verticalRotation -= currentMouseDelta.y;
    //
    //     verticalRotation = Mathf.Clamp(
    //         verticalRotation,
    //         minVerticalAngle,
    //         maxVerticalAngle
    //     );
    //
    //     // Применяем только X, не трогая Y и Z
    //     Vector3 localRotation = transform.localEulerAngles;
    //
    //     localRotation.x = verticalRotation;
    //
    //     transform.localEulerAngles = localRotation;
    // }
    //
    // private void LockCursor()
    // {
    //     Cursor.lockState = CursorLockMode.Locked;
    //     Cursor.visible = false;
    // }
    //
    // private void UnlockCursor()
    // {
    //     Cursor.lockState = CursorLockMode.None;
    //     Cursor.visible = true;
    // }
    //
    // private float NormalizeAngle(float angle)
    // {
    //     if (angle > 180f)
    //         angle -= 360f;
    //
    //     return angle;
    // }



}
