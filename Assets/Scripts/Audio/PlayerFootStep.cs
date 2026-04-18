using StarterAssets;
using UnityEngine;

namespace LSP.Gameplay.Audio
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterController))]
    public class PlayerFootStep : MonoBehaviour
    {
        [Header("Audio")]
        [SerializeField] private AudioSource footstepAudioSource;
        [SerializeField] private AudioClip footstepClip;

        [Header("Playback")]
        [Min(0f)]
        [SerializeField] private float movementThreshold = 0.1f;
        [Range(0f, 1f)]
        [SerializeField] private float maxVolume = 0.35f;
        [Min(0f)]
        [SerializeField] private float minPitch = 0.95f;
        [Min(0f)]
        [SerializeField] private float maxPitch = 1.1f;
        [Min(0f)]
        [SerializeField] private float fadeInSpeed = 8f;
        [Min(0f)]
        [SerializeField] private float fadeOutSpeed = 12f;
        [SerializeField] private bool requireGrounded = true;

        [Header("References")]
        [SerializeField] private CharacterController characterController;
        [SerializeField] private FirstPersonController firstPersonController;

        private void Awake()
        {
            if (characterController == null)
            {
                characterController = GetComponent<CharacterController>();
            }

            if (firstPersonController == null)
            {
                firstPersonController = GetComponent<FirstPersonController>();
            }

            if (footstepAudioSource == null)
            {
                footstepAudioSource = GetComponent<AudioSource>();
            }

            if (footstepAudioSource == null)
            {
                footstepAudioSource = gameObject.AddComponent<AudioSource>();
            }

            footstepAudioSource.loop = true;
            footstepAudioSource.playOnAwake = false;
            footstepAudioSource.spatialBlend = 0f;
            footstepAudioSource.volume = 0f;

            if (footstepAudioSource.clip == null && footstepClip != null)
            {
                footstepAudioSource.clip = footstepClip;
            }
        }

        private void Update()
        {
            if (footstepAudioSource == null || footstepClip == null || characterController == null)
            {
                return;
            }

            if (footstepAudioSource.clip != footstepClip)
            {
                footstepAudioSource.clip = footstepClip;
            }

            Vector3 horizontalVelocity = characterController.velocity;
            horizontalVelocity.y = 0f;
            float speed = horizontalVelocity.magnitude;

            bool grounded = !requireGrounded;
            if (!grounded)
            {
                grounded = firstPersonController != null
                    ? firstPersonController.Grounded
                    : characterController.isGrounded;
            }

            bool shouldPlay = grounded && speed > movementThreshold;
            if (shouldPlay)
            {
                if (!footstepAudioSource.isPlaying)
                {
                    footstepAudioSource.Play();
                }

                float maxSpeed = firstPersonController != null
                    ? Mathf.Max(firstPersonController.SprintSpeed, 0.01f)
                    : 6f;
                float normalizedSpeed = Mathf.Clamp01(speed / maxSpeed);

                float targetVolume = maxVolume * normalizedSpeed;
                float targetPitch = Mathf.Lerp(minPitch, maxPitch, normalizedSpeed);

                footstepAudioSource.volume = Mathf.MoveTowards(
                    footstepAudioSource.volume,
                    targetVolume,
                    fadeInSpeed * Time.deltaTime);
                footstepAudioSource.pitch = targetPitch;
                return;
            }

            footstepAudioSource.volume = Mathf.MoveTowards(
                footstepAudioSource.volume,
                0f,
                fadeOutSpeed * Time.deltaTime);

            if (footstepAudioSource.isPlaying && footstepAudioSource.volume <= 0.001f)
            {
                footstepAudioSource.Stop();
            }
        }
    }
}
