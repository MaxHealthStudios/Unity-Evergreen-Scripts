using UnityEngine;
using UnityEngine.InputSystem;

/* HOW TO USE:
 - All environments must be vertical or horzizontal, nothing diagonal.
 - All environments must have the layer "Environment".
 - One-way platforms must include the tag "one way", and must be with position.z = -11 or lower. If not, there's code to make it -11.
 
INSPECTOR VALUES RECOMMENDATIONS (feel free to experiment):
- speed 10, acceleration 5, deceleration 10
- jumpForce 30, coyoteTime 0.2, gravity 75, maxFallSpeed 125
- wallFriction 50 (works if wallJumping is ticked)
 */

public class PlatformerController2DFlat : MonoBehaviour {
    private float skinWidth = 0.05f;//Small margin inside the player for raycasting.
    [SerializeField]private float minY, maxY, minX, maxX;//Used to constrain the player position
    private float inputX;
    private Vector2 velocity;//Used to move the player

    public float speed, acceleration, deceleration, jumpForce, coyoteTime, gravity, maxFallSpeed;
    [Range(0, 2)] public int extraJumps;
    public bool jumpHeightControl, infiniteJumping, wallJumping;
    public float wallFriction;

    private float startCoyoteTime, startJumpForce;
    private int startExtraJumps;
    private bool grounded, walled, justJumped;

    void Awake() {
        minX = minY = float.MinValue;
        maxX = maxY = float.MaxValue;
        startCoyoteTime = coyoteTime;
        startJumpForce = jumpForce;
        startExtraJumps = extraJumps;
    }

    void Update() {
        Movement();
        ApplyGravity();
        RaycastDown(3);
        Jumping();
        RaycastUp(3);
        if(transform.position.y >= minY) {//This gives priority to vertical collisions over horizontal ones. Important for platformers.
            RaycastLeft(5);
            RaycastRight(5);
        }
    }

    void LateUpdate() {
        transform.Translate(velocity * Time.deltaTime);
        transform.position = new Vector3(Mathf.Clamp(transform.position.x, minX, maxX), Mathf.Clamp(transform.position.y, minY, maxY), transform.position.z);
    }

    private void Movement() {
        if(Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow) || (Gamepad.current != null && (Gamepad.current.dpad.left.isPressed || Gamepad.current.leftStick.left.isPressed))) {//LEFT INPUT
            inputX = inputX > 0 ? 0 /*Snap to 0 if needed.*/ : Mathf.MoveTowards(inputX, -1, acceleration * Time.deltaTime);
        } else if(Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow) || (Gamepad.current != null && (Gamepad.current.dpad.right.isPressed || Gamepad.current.leftStick.right.isPressed))) {//RIGHT INPUT
            inputX = inputX < 0 ? 0 /*Snap to 0 if needed.*/ : Mathf.MoveTowards(inputX, 1, acceleration * Time.deltaTime);
        } else {//Decelerate
            inputX = Mathf.MoveTowards(inputX, 0, deceleration * Time.deltaTime);
        }

        velocity.x = speed * inputX;

        if((transform.position.x <= minX && inputX < 0) || (transform.position.x >= maxX && inputX > 0)) {
            velocity.x = 0;
        }
    }
    void ApplyGravity() {
        walled = wallJumping == true && grounded == false && inputX != 0 && velocity.x == 0 && velocity.y < 0 ? true : false;//This needs to be here, before gravity gets applied.

        if(transform.position.y > minY) {
            grounded = false;
            coyoteTime -= Time.deltaTime;
            velocity.y = Mathf.Clamp(velocity.y - gravity * Time.deltaTime, -maxFallSpeed / (walled == true ? wallFriction : 1), velocity.y);
        } else {
            if(grounded == false) {
                velocity.y = 0;
                coyoteTime = startCoyoteTime;
                extraJumps = startExtraJumps;
                grounded = true;
            }
        }

        if(walled == true) {
            coyoteTime = startCoyoteTime;//Can help prevent wasting an air jump if you accidently go off the wall before jumping.
            extraJumps = startExtraJumps;
        }
    }
    void Jumping() {
        if(Input.GetKeyDown(KeyCode.Space) || (Gamepad.current != null && Gamepad.current.buttonSouth.wasPressedThisFrame)) {
            if(jumpForce > 0 && walled == false && (grounded == true || coyoteTime > 0)) {//Normal jump
                justJumped = true;
                velocity.y = jumpForce;
                coyoteTime = 0;
                grounded = false;
            } else if(grounded == true && jumpForce == 0) {//Jump down through one-way platform (condition for this is met in the 'RaycastDown' method)
                justJumped = true;
                coyoteTime = 0;
                minY = minY - skinWidth * 2;
                transform.position = new Vector3(transform.position.x, transform.position.y - skinWidth * 2, transform.position.z);
            } else if(infiniteJumping == true && grounded == false && velocity.y < 0) {//Infinite air jump
                justJumped = true;
                velocity.y = jumpForce;
            } else if(infiniteJumping == false && extraJumps > 0 && coyoteTime <= 0 && grounded == false && velocity.y < 0) {//Extra air jumps
                justJumped = true;
                extraJumps--;
                velocity.y = jumpForce;
            } else if(wallJumping == true && walled == true) {//Wall Jumping
                justJumped = true;
                coyoteTime = 0;
                velocity.y = jumpForce;
            }
        }

        //JUMP HEIGHT CONTROL
        if(jumpHeightControl == true && justJumped == true) {
            if(Input.GetKeyUp(KeyCode.Space) || (Gamepad.current != null && Gamepad.current.buttonSouth.wasReleasedThisFrame)) {
                velocity.y /= 2.5f;
                justJumped = false;
            }
        }
    }

    void RaycastDown(int rayCount) {
        transform.localScale = new Vector3(1, 2, 1);
        minY = float.MinValue;
        jumpForce = startJumpForce;
        Vector2 rayOrigin = new Vector2(transform.position.x - transform.lossyScale.x / 2 + skinWidth, transform.position.y - transform.lossyScale.y / 2 + skinWidth);
        float raySpacing = (transform.lossyScale.x - skinWidth * 2) / (rayCount - 1);
        float rayLength = gravity;

        for(int i = 0; i < rayCount; i++) {
            RaycastHit2D hit = Physics2D.Raycast(rayOrigin + new Vector2(raySpacing * i, 0), Vector2.down, rayLength, LayerMask.GetMask("Environment"));
            Debug.DrawRay(rayOrigin + new Vector2(raySpacing * i, 0), Vector2.down * rayLength, Color.red);

            if(hit == true) {
                float playerBottom = transform.position.y + skinWidth - transform.lossyScale.y / 2;
                float platformTop = hit.transform.position.y + hit.transform.lossyScale.y / 2;
                if(hit.transform.tag != "one way" || (hit.transform.tag == "one way" && playerBottom > platformTop)) {
                    float hitMinY = platformTop + transform.lossyScale.y / 2;
                    minY = minY < hitMinY ? hitMinY : minY;
                }
            }

            //CONDITION FOR JUMPING DOWN THROUGH ONE-WAY PLATFORMS
            if(hit == true && hit.transform.tag == "one way" && velocity.y <= 0 && transform.position.y <= minY) {
                if(inputX == 0 && (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow) || (Gamepad.current != null && (Gamepad.current.dpad.down.isPressed || Gamepad.current.leftStick.down.isPressed)))) {
                    jumpForce = 0;
                }
            }
        }
    }
    void RaycastUp(int rayCount) {
        maxY = float.MaxValue;
        Vector2 rayOrigin = new Vector2(transform.position.x - transform.lossyScale.x / 2 + skinWidth, transform.position.y + transform.lossyScale.y / 2 - skinWidth);
        float raySpacing = (transform.lossyScale.x - skinWidth * 2) / (rayCount - 1);
        float rayLength = jumpForce;

        for(int i = 0; i < rayCount; i++) {
            RaycastHit2D hit = Physics2D.Raycast(rayOrigin + new Vector2(raySpacing * i, 0), Vector2.up, rayLength, LayerMask.GetMask("Environment"), -10);
            Debug.DrawRay(rayOrigin + new Vector2(raySpacing * i, 0), Vector2.up * rayLength, Color.red);

            if(hit == true && hit.transform.tag == "one way") {
                hit.transform.position = new Vector3(hit.transform.position.x, hit.transform.position.y, -11);
            }

            if(hit == true && hit.transform.tag != "one way") {
                float hitMaxY = hit.transform.position.y - hit.transform.lossyScale.y / 2 - transform.lossyScale.y / 2;
                maxY = maxY > hitMaxY ? hitMaxY : maxY;
                if(transform.position.y == maxY) {
                    velocity.y /= 2;
                }
            }
        }
    }
    void RaycastLeft(int rayCount) {
        minX = float.MinValue;
        Vector2 rayOrigin = new Vector2(transform.position.x - transform.lossyScale.x / 2 + skinWidth, transform.position.y - transform.lossyScale.y / 2 + skinWidth);
        float raySpacing = (transform.lossyScale.y - skinWidth * 2) / (rayCount - 1);
        float rayLength = speed;

        for(int i = 0; i < rayCount; i++) {
            RaycastHit2D hit = Physics2D.Raycast(rayOrigin + new Vector2(0, raySpacing * i), Vector2.left, rayLength, LayerMask.GetMask("Environment"));
            Debug.DrawRay(rayOrigin + new Vector2(0, raySpacing * i), Vector2.left * rayLength, Color.red);
  
            if(hit == true && hit.transform.tag != "one way") {
                if(grounded == false || i > 0) {
                    float hitMinX = hit.transform.position.x + hit.transform.lossyScale.x / 2 + transform.lossyScale.x / 2;
                    minX = minX < hitMinX ? hitMinX : minX;
                }
            }
        }
    }
    void RaycastRight(int rayCount) {
        maxX = float.MaxValue;
        Vector2 rayOrigin = new Vector2(transform.position.x + transform.lossyScale.x / 2 - skinWidth, transform.position.y - transform.lossyScale.y / 2 + skinWidth);
        float raySpacing = (transform.lossyScale.y - skinWidth * 2) / (rayCount - 1);
        float rayLength = speed;

        for(int i = 0; i < rayCount; i++) {
            RaycastHit2D hit = Physics2D.Raycast(rayOrigin + new Vector2(0, raySpacing * i), Vector2.right, rayLength, LayerMask.GetMask("Environment"));
            Debug.DrawRay(rayOrigin + new Vector2(0, raySpacing * i), Vector2.right * rayLength, Color.red);

            if(hit == true && hit.transform.tag != "one way") {
                if(grounded == false || i > 0) {
                    float hitMaxX = hit.transform.position.x - hit.transform.lossyScale.x / 2 - transform.lossyScale.x / 2;
                    maxX = maxX > hitMaxX ? hitMaxX : maxX;
                }
            }
        }
    }
}