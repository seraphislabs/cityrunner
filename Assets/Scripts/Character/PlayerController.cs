using Fusion;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UIElements;

public class PlayerController: NetworkBehaviour
{
    public float speed = 2.0f; // Speed of the character
    private float actualSpeed = 2.0f;
    public float gravity = -9.81f; // Gravity force applied to the character
    public float jumpHeight = 1.5f; // Height of the jump
    public float cameraSnapLerpSpeed = 0.1f;
    public float rotationLerpSpeed = 5.0f;
    public CharacterController controller;
    public Animator animator;
    public bool mouseLocked = true;
    private Quaternion targetRotation;

    private GameObject cameraAnchor;

    private Vector3 velocity; // Velocity for gravity application
    private Vector3 lastPosition; // Last position of the character
    private bool isGrounded;

    // Variables to store forward and sideways velocity
    private float forwardVelocity;
    private float sidewaysVelocity;

    private bool cameraControl = true;

    public void Start()
    {
        targetRotation = transform.rotation; // Initialize targetRotation
        Debug.Log("CharacterController started");
    }

    public void Awake()
    {
        // Initialize the character controller
        controller = GetComponent<CharacterController>();
        animator = GetComponent<Animator>();
        cameraAnchor = GameObject.Find("CameraAnchor");
        Debug.Log("CharacterController awaked");
    }

    public void Update() {
        // Check if the player is on the ground
        isGrounded = controller.isGrounded;

        // If the player is on the ground and the downward velocity is less than 0, reset it
        if (isGrounded && velocity.y < 0)
        {
            velocity.y = -2f; // Small negative value to ensure the character stays grounded
        }

        // Get the input from the horizontal and vertical axis (WASD or Arrow keys)
        float x = Input.GetAxis("Horizontal"); // Strafe input
        float z = Input.GetAxis("Vertical");   // Forward input

        if (z < 0) {
            actualSpeed = speed * 0.75f;
        } else {
            actualSpeed = speed;
        }

        // Calculate the movement direction relative to the character's orientation
        Vector3 move = transform.right * x + transform.forward * z;

        // Directly check if there is any input movement
        if (move.magnitude > 0.001f)
        {
            // Normalize the move vector to maintain consistent speed
            move.Normalize();
        }
        else
        {
            // If no movement input is detected, set move to zero vector
            move = Vector3.zero;
        }

        // Move the character controller in the desired direction
        controller.Move(move * actualSpeed * Time.deltaTime);

        // Apply gravity to the character
        velocity.y += gravity * Time.deltaTime;

        // If the jump key (space bar) is pressed and the player is on the ground, apply upward velocity
        if (Input.GetButtonDown("Jump") && isGrounded)
        {
            velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
        }

        // Move the character downwards due to gravity
        controller.Move(velocity * Time.deltaTime);

        // Calculate the actual velocity based on position change
        Vector3 currentPosition = transform.position;
        Vector3 movement = currentPosition - lastPosition;

        // Calculate forward and sideways velocity
        forwardVelocity = Vector3.Dot(movement / Time.deltaTime, transform.forward);
        sidewaysVelocity = Vector3.Dot(movement / Time.deltaTime, transform.right);

        // Update the lastPosition for the next frame
        lastPosition = currentPosition;

        if (Input.GetKey(KeyCode.Q)) {
            if (!mouseLocked) {
                // Set target rotation to turn left
                targetRotation = Quaternion.Euler(transform.eulerAngles + Vector3.up * -7);
            }
            // Smoothly interpolate the rotation towards the target rotation
            transform.rotation = Quaternion.Lerp(transform.rotation, targetRotation, Time.deltaTime * 15);
        }

        if (Input.GetKey(KeyCode.E)) {
            if (!mouseLocked) {
                // Set target rotation to turn right
                targetRotation = Quaternion.Euler(transform.eulerAngles + Vector3.up * 7);
            }
            // Smoothly interpolate the rotation towards the target rotation
            transform.rotation = Quaternion.Lerp(transform.rotation, targetRotation, Time.deltaTime * 15);
        }

        BasicControls();
        UpdateAnimator();
        UpdateAimPoint();
        UpdateCamnera();
    }

    void UpdateAnimator() {
        // Set the forward and sideways velocity in the animator
        int forwardDirection = 0;
        int sidewaysDirection = 0;
        float threshold = 0.1f; // Define a small threshold value

        if (Mathf.Abs(forwardVelocity) < threshold) {
            forwardDirection = 0; // Set to zero if within the threshold
        } else if (forwardVelocity < 0) {
            forwardDirection = -1;
        } else if (forwardVelocity > 0) {
            forwardDirection = 1;   
        }

        if (Mathf.Abs(sidewaysVelocity) < threshold) {
            sidewaysDirection = 0; // Set to zero if within the threshold
        } else if (sidewaysVelocity < 0) {
            sidewaysDirection = -1;
        } else if (sidewaysVelocity > 0) {
            sidewaysDirection = 1;
        }

        animator.SetInteger("ForwardDirection", forwardDirection);
        animator.SetInteger("SidewaysDirection", sidewaysDirection);
        animator.SetFloat("ForwardSpeed", Mathf.Abs(forwardVelocity));
        animator.SetFloat("SidewaysSpeed", Mathf.Abs(sidewaysVelocity));
    }

    void UpdateCamnera() {
    if (cameraControl) {
        Camera.main.transform.position = Vector3.Lerp(
            Camera.main.transform.position, 
            cameraAnchor.transform.position, 
            Time.deltaTime * cameraSnapLerpSpeed
            );

        Camera.main.transform.rotation = Quaternion.Lerp(
            Camera.main.transform.rotation, 
            cameraAnchor.transform.rotation, 
            Time.deltaTime * cameraSnapLerpSpeed
            );
        }
    }

    void UpdateAimPoint() {
        if (mouseLocked) {
            // Rotate character to where mouse is pointing on screen
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;
            if (Physics.Raycast(ray, out hit)) {
                Vector3 lookAt = new Vector3(hit.point.x, transform.position.y, hit.point.z);
                
                // Calculate direction to the hit point
                Vector3 directionToLookAt = (lookAt - transform.position).normalized;
                
                // Calculate the angle between the forward direction and the direction to the hit point
                float angle = Vector3.Angle(transform.forward, directionToLookAt);
                
                // Define the deadzone angle threshold
                float deadzoneAngle = 8.0f; // Adjust this value as needed
                
                // Rotate the character only if the angle is outside the deadzone
                if (angle > deadzoneAngle) {
                    // Calculate the target rotation
                    Quaternion targetRotation = Quaternion.LookRotation(directionToLookAt);
                    
                    // Interpolate the rotation
                    transform.rotation = Quaternion.Lerp(transform.rotation, targetRotation, Time.deltaTime * rotationLerpSpeed);
                }
            }
        }
    }

    void BasicControls() {
        // if right mouse is being held
        if (Input.GetMouseButton(1)) {
            // if the mouse is unlocked, lock it
            if (!mouseLocked) {
                mouseLocked = true;
            }
        } else {
            // if the mouse is locked, unlock it
            if (mouseLocked) {
                mouseLocked = false;
            }
        }
    }
}
