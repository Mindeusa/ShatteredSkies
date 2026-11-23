using UnityEngine;

namespace Player
{
    public class PlayerController : MonoBehaviour
    {
        public float moveSpeed = 5f;
        public float jumpForce = 5f;
        private Rigidbody rb;
        void Start()
        {
            rb = GetComponent<Rigidbody>();
        }

        // Update is called once per frame
        void Update()
        {
            Move();

            if (Input.GetKeyDown(KeyCode.Space))
            {
                Jump();
            }
        }

        void Move()
        {
            float moveHorisontal = Input.GetAxis("Horizontal");
            float moveVertical = Input.GetAxis("Vertical");

            Vector3 movement = new Vector3(moveHorisontal, 0, moveVertical) * moveSpeed * Time.deltaTime;
            transform.Translate(movement, Space.Self);
        }

        void Jump()
        {
            if(Mathf.Abs(rb.linearVelocity.y) < 0.001f)
            {
                rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
            }
        }
    }
}
