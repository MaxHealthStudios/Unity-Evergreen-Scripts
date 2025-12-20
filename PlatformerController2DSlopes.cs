using UnityEngine;
using UnityEngine.InputSystem;

/* HOW TO USE:
 * 
 * You can use 3 layers for the player collisions:
 * - "one way" (layer 6) only collides with raycasts down
 * - "wall" (layer 7) only collides with raycasts left/right
 * - "full" (layer 8) collides with all raycasts
 *
 * INSPECTOR EXAMPLE VALUES:
 * - 'speed' seems ideal between 7-12
 * - 'acceleration' feels good around 2-5
 * - 'gravity' around 75 and 'jumpForce' around 30 feel good.
 * - 'coyoteTime' works well at 0.2f - 0.25f
 * - 'wallFriction' at 20-50 feels great. Used to slow down your descend against a wall. 
 */

/* WEAKNESSES OF THIS SCRIPT:
 * - The player can snap to the ground when falling, depending on the 0.025f number in the 'GroundCollisions' method. You can decrease to 0.01f if you want, but we need some amount for going down slopes :(((
 * - The player can snap to a platform when jumping onto it from below. This is again part of the 'GroundCollisions' method, namely, in the way 'hitPointDownL' and 'hitPointDownR' are calculated. You can try overcome this by making the platforms really thin and placing them a bit lower. If you try to overcome this with more code, the player can start falling through slopes.
 * - The player can fly off when going down slopes if they are too steep or your speed is too high.
 * - Collision issues when hitting a ceiling while climbing a steep slope (above 45 degrees).
 * - The player can glitch if jumping down through one-way platform that are too close.
 * - Walls need to be vertical, sloped walls produce glitches. Use slopes only for platforms, not walls.
 */

public class PlatformerController2DSlopes : MonoBehaviour {
    [Min(0.25f)] public float speed, acceleration;
    public float gravity, jumpForce, coyoteTime, wallFriction;
    private float startCoyoteTime, startJumpForce;
    public bool wallJumping, jumpHeightControl;
    [Range(0, 2)]public int extraJumps;
    private int startExtraJumps;

    private Vector2 velocity;
    private float inputX, minX, maxX, minY, maxY;
    private float hitPointDownL, hitPointDownR, hitPointUpL, hitPointUpR;
    private bool grounded, walled;

    private GameObject oneWayPlatform;
    private bool jumpingDown;
    private float jumpDownTime, startJumpDownTime;

    void Awake() {
        minX = minY = float.MinValue;
        maxX = maxY = float.MaxValue;
        startCoyoteTime = coyoteTime;
        startExtraJumps = extraJumps;
        startJumpForce = jumpForce;
        jumpDownTime = startJumpDownTime = 0.1f;
    }

    void Update() {
        HorizontalVelocity();
        HorizontalCollisions(5, 0.05f);
        ApplyGravity();
        OneWayPlatforms(transform.lossyScale.y / 2.5f);
        GroundCollisions(0.05f);
        CeilingCollisions(0.05f);
        Jumping();
        SpecialJumping();
    }

    void FixedUpdate() {
        transform.Translate(velocity * Time.fixedDeltaTime);
        transform.position = new Vector3(Mathf.Clamp(transform.position.x, minX, maxX), Mathf.Clamp(transform.position.y, minY, maxY), transform.position.z);
    }

    void LateUpdate() {
        transform.position = new Vector3(Mathf.Clamp(transform.position.x, minX, maxX), grounded == true ? minY : Mathf.Clamp(transform.position.y, minY, maxY), transform.position.z);
    }

    private void HorizontalVelocity() {
        if(Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow) || (Gamepad.current != null && (Gamepad.current.dpad.left.isPressed || Gamepad.current.leftStick.left.isPressed))) {//LEFT INPUT
            inputX = inputX > 0 ? 0 /*Snap to 0 if needed.*/ : Mathf.MoveTowards(inputX, -1, acceleration * Time.deltaTime);
        } else if(Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow) || (Gamepad.current != null && (Gamepad.current.dpad.right.isPressed || Gamepad.current.leftStick.right.isPressed))) {//RIGHT INPUT
            inputX = inputX < 0 ? 0 /*Snap to 0 if needed.*/ : Mathf.MoveTowards(inputX, 1, acceleration * Time.deltaTime);
        } else {//Decelerate
            float deceleration = 2.5f * acceleration;
            inputX = Mathf.MoveTowards(inputX, 0, deceleration * Time.deltaTime);
        }

        velocity.x = speed * inputX;
    }
    private void HorizontalCollisions(int rayCount, float skinWidth) {//Raycast Left/Right
        minX = float.MinValue;
        maxX = float.MaxValue;
        if(inputX != 0) {
            Vector2 pos = new Vector2(transform.position.x, transform.position.y);
            Vector2 size = new Vector2(transform.lossyScale.x, transform.lossyScale.y);
            Vector2 bottomLeft = new Vector2(pos.x - size.x / 2 + skinWidth, pos.y - size.y / 2 + skinWidth);
            Vector2 bottomRight = new Vector2(pos.x + size.x / 2 - skinWidth, pos.y - size.y / 2 + skinWidth);
            Vector2 raySpacing = new Vector2(0, transform.lossyScale.y - skinWidth * 2) / (rayCount - 1);
            float rayLength = speed;

            for(int i = 0; i < rayCount; i++) {
                RaycastHit2D hit = Physics2D.Raycast((inputX < 0 ? bottomLeft : bottomRight) + raySpacing * i, inputX < 0 ? Vector2.left : Vector2.right, rayLength, LayerMask.GetMask("wall", "full"));
                Debug.DrawRay((inputX < 0 ? bottomLeft : bottomRight) + raySpacing * i, (inputX < 0 ? Vector2.left : Vector2.right) * rayLength, Color.cyan);
                
                bool squished = (Mathf.Abs(hitPointUpR - hitPointDownR) <= transform.lossyScale.y || Mathf.Abs(hitPointUpL - hitPointDownL) <= transform.lossyScale.y);//This is important for removing a nasty glitch when climbing slopes and hitting a ceiling. And also for allowing the climbing of stairs and slopes.

                if(hit == true && (grounded == false || ((squished == false && i > 0) || (squished == true && i < rayCount - 1)))) {
                    rayLength = hit.distance + skinWidth;
                    float xLimit = hit.point.x - Mathf.Sign(inputX) * (transform.lossyScale.x / 2f);
                    xLimit = (float)System.Math.Round(xLimit, 3);
                    minX = inputX < 0 ? xLimit : float.MinValue;
                    maxX = inputX > 0 ? xLimit : float.MaxValue;
                    if((inputX < 0 && transform.position.x <= minX) || (inputX > 0 && transform.position.x >= maxX)) {
                        transform.position = new Vector3(Mathf.Clamp(transform.position.x, minX, maxX), transform.position.y, transform.position.z);
                        velocity.x = 0;
                    }
                }
            }
        }
    }
    private void ApplyGravity() {
        walled = wallJumping == true && grounded == false && inputX != 0 && velocity.x == 0 ? true : false;//This needs to be here, before gravity gets applied.
        coyoteTime = walled == true ? startCoyoteTime : coyoteTime;//Coyote time can help to prevent wasting an air jump if you accidently go off the wall before jumping.

        if(grounded == false) {
            velocity.y -= gravity * Time.deltaTime;
            velocity.y = Mathf.Clamp(velocity.y, walled == false ? -gravity : -gravity/wallFriction, float.MaxValue);
        }
    }
    private void OneWayPlatforms(float rayLength) {
        Vector2 originLeft = new Vector2(transform.position.x - transform.lossyScale.x / 2, transform.position.y);
        Vector2 originRight = new Vector2(transform.position.x + transform.lossyScale.x / 2, transform.position.y);
        RaycastHit2D hitLeft = Physics2D.Raycast(originLeft, Vector2.down, rayLength, LayerMask.GetMask("one way", "UI"));
        RaycastHit2D hitRight = Physics2D.Raycast(originRight, Vector2.down, rayLength, LayerMask.GetMask("one way", "UI"));
        Debug.DrawRay(originLeft, Vector2.down * rayLength, Color.green);
        Debug.DrawRay(originRight, Vector2.down * rayLength, Color.green);

        if(hitLeft == true || hitRight == true) {
            if(oneWayPlatform != null) {
                oneWayPlatform.layer = 6;
                oneWayPlatform = null;
            }
            oneWayPlatform = hitLeft == true ? hitLeft.transform.gameObject : hitRight.transform.gameObject;
            oneWayPlatform.layer = 5;
        } else if(hitLeft == false && hitRight == false) {
            if(oneWayPlatform != null) {
                oneWayPlatform.layer = 6;
                oneWayPlatform = null;
            }
        }
    }
    private void GroundCollisions(float skinWidth) {//Raycast Down
        float rayLength = gravity;
        Vector2 originLeft = new Vector2(transform.position.x - transform.lossyScale.x / 2 + skinWidth, transform.position.y);
        Vector2 originRight = new Vector2(transform.position.x + transform.lossyScale.x / 2 - skinWidth, transform.position.y);
        RaycastHit2D hitLeft = Physics2D.Raycast(originLeft, Vector2.down, rayLength, LayerMask.GetMask("one way", "full"));
        RaycastHit2D hitRight = Physics2D.Raycast(originRight, Vector2.down, rayLength, LayerMask.GetMask("one way", "full"));
        Debug.DrawRay(originLeft, Vector2.down * rayLength, Color.red);
        Debug.DrawRay(originRight, Vector2.down * rayLength, Color.red);

        hitPointDownL = jumpingDown == false && hitLeft == true && velocity.y <= 0 ? (float)System.Math.Round(hitLeft.point.y, 3) : float.MinValue;
        hitPointDownR = jumpingDown == false && hitRight == true && velocity.y <= 0 ? (float)System.Math.Round(hitRight.point.y, 3) : float.MinValue;
        minY = Mathf.Max(hitPointDownL, hitPointDownR) + transform.lossyScale.y / 2;
        minY = (float)System.Math.Round(minY, 3);

        transform.position = new Vector3(transform.position.x, Mathf.Clamp(transform.position.y, minY, maxY), transform.position.z);

        if(grounded == false) {
            if(Mathf.Abs(transform.position.y - minY) <= 0.025f + Mathf.Min(0.5f, Mathf.Abs(hitPointDownL- hitPointDownR)) && velocity.y <= 0) {// "velocity.y <= 0" is very important, without it, jumping is automatically neutralized
                velocity.y = 0;
                coyoteTime = startCoyoteTime;
                transform.position = new Vector3(transform.position.x, minY, transform.position.z);
                extraJumps = startExtraJumps;
                grounded = true;
                //Debug.Log("Landed!");
            }
        } else {
            //When walking off a platform without jumping:
            if(Mathf.Abs(transform.position.y - minY) > 0.5f) {//The higher the value, the more your player will stick to downward slopes.
                grounded = false;
            }
        }

        //JUMP DOWN
        if(((hitLeft == true && hitLeft.transform.gameObject.layer == 6) || (hitRight == true && hitRight.transform.gameObject.layer == 6)) && grounded == true && inputX == 0 && (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow) || (Gamepad.current != null && (Gamepad.current.dpad.down.isPressed || Gamepad.current.leftStick.down.isPressed)))) {
            jumpForce = 0;
            if(Input.GetKeyDown(KeyCode.Space) || (Gamepad.current != null && Gamepad.current.buttonSouth.wasPressedThisFrame)) {
                if(jumpingDown == false) {
                    jumpingDown = true;
                }
            }
        } else {
            jumpForce = startJumpForce;
        }

        if(jumpingDown == true) {
            if(jumpDownTime > 0) {
                jumpDownTime -= Time.deltaTime;
            } else {
                jumpingDown = false;
                jumpDownTime = startJumpDownTime;
            }
        }
    }
    private void CeilingCollisions(float skinWidth) {//Raycast Up
        float rayLength = jumpForce;
        Vector2 leftOrigin = new Vector2(transform.position.x - transform.lossyScale.x / 2 + skinWidth, transform.position.y + transform.lossyScale.y/8);
        Vector2 rightOrigin = new Vector2(transform.position.x + transform.lossyScale.x / 2 - skinWidth, transform.position.y + transform.lossyScale.y/8);
        RaycastHit2D hitLeft = Physics2D.Raycast(leftOrigin, Vector2.up, rayLength, LayerMask.GetMask("full"));
        RaycastHit2D hitRight = Physics2D.Raycast(rightOrigin, Vector2.up, rayLength, LayerMask.GetMask("full"));
        Debug.DrawRay(leftOrigin, Vector2.up * rayLength, Color.red);
        Debug.DrawRay(rightOrigin, Vector2.up * rayLength, Color.red);

        hitPointUpL = hitLeft == true ? (float)System.Math.Round(hitLeft.point.y, 3) : float.MaxValue;
        hitPointUpR = hitRight == true ? (float)System.Math.Round(hitRight.point.y, 3) : float.MaxValue;

        maxY = hitLeft == false && hitRight == false ? float.MaxValue
             : hitLeft == true && hitRight == true ? Mathf.Min(hitLeft.point.y, hitRight.point.y)
             : hitLeft == true ? hitLeft.point.y : hitRight.point.y;
        maxY = (float)System.Math.Round(maxY, 3) - transform.lossyScale.y / 2;
        maxY = Mathf.Max(minY, maxY);

        transform.position = new Vector3(transform.position.x, Mathf.Min(transform.position.y, maxY), transform.position.z);

        //Stopping the jump if a ceiling is hit.
        if(Mathf.Abs(transform.position.y - maxY) < 0.05f && velocity.y > 0) {
            velocity.y = 0;
        }

        //This long if statement is very important. It stops movement if you hit a ceiling while climbing slopes.
        if((inputX > 0 && Mathf.Abs(hitPointUpR - hitPointDownR) <= transform.lossyScale.y) || (inputX < 0 && Mathf.Abs(hitPointUpL - hitPointDownL) <= transform.lossyScale.y)) {
            velocity.x = 0;
        }
    }
    private void Jumping() {
        if(jumpForce > 0 && coyoteTime > 0 && Mathf.Abs(hitPointUpR - hitPointDownR) > transform.lossyScale.y && Mathf.Abs(hitPointUpL - hitPointDownL) > transform.lossyScale.y) {
            //Debug.Log("Can Jump");
            if(Input.GetKeyDown(KeyCode.Space) || (Gamepad.current != null && Gamepad.current.buttonSouth.wasPressedThisFrame)) {
                velocity.y = jumpForce;
                grounded = false;
                coyoteTime = 0;
                //Debug.Log("Jumped!");
            }
        }
        if(grounded == false) {
            coyoteTime -= Time.deltaTime;
            if(jumpHeightControl == true && velocity.y > 0 && (Input.GetKeyUp(KeyCode.Space) || (Gamepad.current != null && Gamepad.current.buttonSouth.wasReleasedThisFrame))) {
                velocity.y = gravity * Time.fixedDeltaTime;
            }
        }

        
    }
    private void SpecialJumping() {
        //Remove "extraJumps > 0 &&" for infinite jumping.
        if(extraJumps > 0 && walled == false && grounded == false && coyoteTime <= 0 && jumpForce > 0) {//EXTRA JUMPS
            if(velocity.y <= 0 && (Input.GetKeyDown(KeyCode.Space) || (Gamepad.current != null && Gamepad.current.buttonSouth.wasPressedThisFrame))) {
                velocity.y = jumpForce;
                extraJumps--;
            }
        }

        if(wallJumping == true && walled == true) {//WALL JUMPING
            extraJumps = startExtraJumps;
            if(velocity.y <= 0 && (Input.GetKeyDown(KeyCode.Space) || (Gamepad.current != null && Gamepad.current.buttonSouth.wasPressedThisFrame))) {
                velocity.y = jumpForce;
                walled = false;
            }
        }
    }
}