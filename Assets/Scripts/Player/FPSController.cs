using UnityEngine;

namespace Player
{
    [RequireComponent(typeof(CharacterController))]
    public class FPSController : MonoBehaviour
    {
        public Camera playerCamera;
        public float walkSpeed = 3f;
        public float runSpeed = 6f;
        public float jumpPower = 3.5f;
        public float gravity = 5f;

        public float lookSpeed = 2f;
        public float lookXLimit = 85f; // Uppdaterad till 85 för att kunna titta upp/ner på himmel/mark

        Vector3 moveDir = Vector3.zero;
        float rotationX = 0;

        public bool canMove = true;

        CharacterController characterController;

        private void Start()
        {
            characterController = GetComponent<CharacterController>();
            
            // Vi behöver inte sätta detectCollisions manuellt här, 
            // det sköts automatiskt av CharacterController.
            // (CapsuleCollider behövs inte om du har CharacterController)

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        private void Update()
        {
            // --- RÖRELSE ---
            Vector3 forward = transform.TransformDirection(Vector3.forward);
            Vector3 right = transform.TransformDirection(Vector3.right);

            bool isRunning = Input.GetKey(KeyCode.LeftShift);
            float currSpeedX = canMove ? (isRunning ? runSpeed : walkSpeed) * Input.GetAxis("Vertical") : 0;
            float currSpeedY = canMove ? (isRunning ? runSpeed : walkSpeed) * Input.GetAxis("Horizontal") : 0;
            
            float movementDirY = moveDir.y;
            moveDir = (forward * currSpeedX) + (right * currSpeedY);

            // --- HOPP & GRAVITATION ---
            if (Input.GetButton("Jump") && characterController.isGrounded)
            {
                moveDir.y = jumpPower;
            }
            else 
            { 
                moveDir.y = movementDirY; 
            }

            if (!characterController.isGrounded) 
            { 
                moveDir.y -= gravity * Time.deltaTime; 
            }

            // --- TILLÄMPA RÖRELSE ---
            characterController.Move(moveDir * Time.deltaTime);

            // --- KAMERA & ROTATION ---
            if (canMove)
            {
                // Titta Upp/Ner (Kameran)
                rotationX -= Input.GetAxis("Mouse Y") * lookSpeed;
                rotationX = Mathf.Clamp(rotationX, -lookXLimit, lookXLimit);
                playerCamera.transform.localRotation = Quaternion.Euler(rotationX, 0, 0);
                
                // Titta Höger/Vänster (Hela spelaren roterar)
                transform.rotation *= Quaternion.Euler(0, Input.GetAxis("Mouse X") * lookSpeed, 0);
            }
        }

        // --- NY METOD: Nollställer farten in i golvet vid teleportering ---
        // Används av WorldManager när spelaren ska spawna.
        public void ResetGravity()
        {
            moveDir.y = 0f;
        }
    }
}