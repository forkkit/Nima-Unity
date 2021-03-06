using System;
using System.IO;
using UnityEngine;
using Nima.Math2D;

namespace Nima.Unity
{
	public class ArcherController : MonoBehaviour
	{
		private struct AimSlice
		{
			public Vec2D point;
			public Vec2D dir;
		}
		const int AimSliceCount = 60;
		const float MixSpeed = 3.5f;

		AimSlice[] m_AimLookup = new AimSlice[AimSliceCount];
		AimSlice[] m_AimWalkingLookup = new AimSlice[AimSliceCount];

		private ActorBaseComponent m_Actor;
		private CustomFloatProperty m_GroundSpeedProperty;
		private CustomBooleanProperty m_IsRunningProperty;
		private Nima.Animation.ActorAnimation m_Idle;
		private Nima.Animation.ActorAnimation m_Aim;
		private Nima.Animation.ActorAnimationInstance m_Walk;
		private Nima.Animation.ActorAnimation m_Run;
		private Nima.Animation.ActorAnimation m_WalkToIdle;
		private float m_HorizontalSpeed;
		private bool m_IsRunning;
		private float m_RunTime;
		//private float m_WalkTime;
		private float m_WalkMix;
		private float m_RunMix;
		private float m_IdleTime;
		private float m_WalkToIdleTime;
		private float m_AimAnimationTime;

		public void Start()
		{
			m_Actor = gameObject.GetComponent<ActorBaseComponent>();
			if(m_Actor != null)
			{
				// Get a game object from the actor, use this to mount items or query for other components.
				// GameObject headColliderGameObject = m_Actor.GetActorGameObject("HeadCollider");
				// if(headColliderGameObject != null)
				// {
				// 	Collider2D collider = headColliderGameObject.GetComponent<Collider2D>();
				// 	if(collider != null)
				// 	{
				// 		// Set it to a trigger, or do something else with it...
				// 		// collider.isTrigger = true;
				// 	}
				// }
				if(m_Actor.ActorInstance != null)
				{
					m_Idle = m_Actor.ActorInstance.GetAnimation("Idle");
					m_Aim = m_Actor.ActorInstance.GetAnimation("Aim2");
					m_Walk = m_Actor.ActorInstance.GetAnimationInstance("Walk");
					m_Run = m_Actor.ActorInstance.GetAnimation("Run");
					m_WalkToIdle = m_Actor.ActorInstance.GetAnimation("WalkToIdle");

					// We made walk an animation instance so it has it's own sense of time which lets it do things like track events.
					m_Walk.AnimationEvent += delegate(object animationInstance, Nima.Animation.AnimationEventArgs args)
					{
						// Event triggered from animation.
					};
					ActorNode characterNode = m_Actor.ActorInstance.GetNode("Character");
					if(characterNode != null)
					{
						m_GroundSpeedProperty = characterNode.GetCustomFloatProperty("GroundSpeed");
						m_IsRunningProperty = characterNode.GetCustomBooleanProperty("IsRunning");
					}
					// Calculate aim slices.
					if(m_Aim != null)
					{
						ActorNode muzzle = m_Actor.ActorInstance.GetNode("Muzzle");
						if(muzzle != null)
						{
							for(int i = 0; i < AimSliceCount; i++)
							{
								float position = i / (float)(AimSliceCount-1) * m_Aim.Duration;
								m_Aim.Apply(position, m_Actor.ActorInstance, 1.0f);
								m_Actor.ActorInstance.Advance(0.0f);
								Mat2D worldTransform = muzzle.WorldTransform;

								AimSlice slice = m_AimLookup[i];

								// Extract forward vector and position.
								slice.dir = new Vec2D();
								Vec2D.Normalize(slice.dir, new Vec2D(worldTransform[0], worldTransform[1]));
								slice.point = new Vec2D(worldTransform[4]*ActorAsset.NimaToUnityScale, worldTransform[5]*ActorAsset.NimaToUnityScale);
								m_AimLookup[i] = slice;
							}
						}
						if(m_Walk != null)
						{
							m_Walk.Time = 0.0f;
							m_Walk.Apply(1.0f);

							for(int i = 0; i < AimSliceCount; i++)
							{
								float position = i / (float)(AimSliceCount-1) * m_Aim.Duration;
								m_Aim.Apply(position, m_Actor.ActorInstance, 1.0f);
								m_Actor.ActorInstance.Advance(0.0f);
								Mat2D worldTransform = muzzle.WorldTransform;

								AimSlice slice = m_AimWalkingLookup[i];

								// Extract forward vector and position.
								slice.dir = new Vec2D();
								Vec2D.Normalize(slice.dir, new Vec2D(worldTransform[0], worldTransform[1]));
								slice.point = new Vec2D(worldTransform[4]*ActorAsset.NimaToUnityScale, worldTransform[5]*ActorAsset.NimaToUnityScale);
								m_AimWalkingLookup[i] = slice;
							}
						}
					}
				}
			}
			m_IdleTime = 0.0f;
		}

		public void Update()
		{
			if(m_Actor == null)
			{
				return;
			}

			if(m_IsRunningProperty != null)
			{
				// Just an example of how to detect if the character is running from the animated properties (not really necessary here but shows how you can use custom properties).
				// Debug.Log("RUNNING " + m_IsRunningProperty.Value);
			}
			float elapsedSeconds = Time.deltaTime;

			Actor actorInstance = m_Actor.ActorInstance;

			float scaleX = 1.0f;//ActorAsset.NimaToUnityScale;
			// Find cursor position in world space.
			Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
			Vector3 actorLocalCursor = m_Actor.gameObject.transform.InverseTransformPoint(ray.origin);
			if(actorLocalCursor[0] < 0.0f)
			{
				scaleX *= -1.0f;
				actorLocalCursor[0] *= -1.0f;
			}

			actorInstance.Root.ScaleX = scaleX;

			// Advance idle animation first.
			if(m_Idle != null)
			{
				m_IdleTime = (m_IdleTime+elapsedSeconds)%m_Idle.Duration;
				m_Idle.Apply(m_IdleTime, m_Actor.ActorInstance, 1.0f);
			}

			// Update input.
			if( m_HorizontalSpeed != -1.0f && (Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.LeftArrow) ))
			{
				m_HorizontalSpeed = -1.0f;
			}
			else if( m_HorizontalSpeed == -1.0f && (Input.GetKeyUp(KeyCode.A) || Input.GetKeyUp(KeyCode.LeftArrow) ))
			{
				m_HorizontalSpeed = 0.0f;
			}
			if( m_HorizontalSpeed != 1.0f && (Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.RightArrow) ))
			{
				m_HorizontalSpeed = 1.0f;
			}
			else if( m_HorizontalSpeed == 1.0f && (Input.GetKeyUp(KeyCode.D) || Input.GetKeyUp(KeyCode.RightArrow) ))
			{
				m_HorizontalSpeed = 0.0f;
			}
			if( !m_IsRunning && (Input.GetKeyDown(KeyCode.LeftShift) || Input.GetKeyDown(KeyCode.RightShift) ))
			{
				m_IsRunning = true;
			}
			else if( m_IsRunning && (Input.GetKeyUp(KeyCode.LeftShift) || Input.GetKeyUp(KeyCode.RightShift) ))
			{
				m_IsRunning = false;
			}

			if(m_HorizontalSpeed != 0.0f)
			{
				if(m_IsRunning)
				{
					if(m_WalkMix > 0.0f)
					{
						m_WalkMix = Math.Max(0.0f, m_WalkMix - elapsedSeconds * MixSpeed);
					}
					if(m_RunMix < 1.0f)
					{
						m_RunMix = Math.Min(1.0f, m_RunMix + elapsedSeconds * MixSpeed);
					}
				}
				else
				{
					if(m_WalkMix < 1.0f)
					{
						m_WalkMix = Math.Min(1.0f, m_WalkMix + elapsedSeconds * MixSpeed);
					}
					if(m_RunMix > 0.0f)
					{
						m_RunMix = Math.Max(0.0f, m_RunMix - elapsedSeconds * MixSpeed);
					}
				}

				m_WalkToIdleTime = 0.0f;
			}
			else
			{
				if(m_WalkMix > 0.0f)
				{
					m_WalkMix = Math.Max(0.0f, m_WalkMix - elapsedSeconds * MixSpeed);
				}
				if(m_RunMix > 0.0f)
				{
					m_RunMix = Math.Max(0.0f, m_RunMix - elapsedSeconds * MixSpeed);
				}
			}

			float moveSpeed = m_IsRunning ? 11.0f : 6.0f;
//			m_Actor.gameObject.transform.
//			actorInstance.Root.X += m_HorizontalSpeed * elapsedSeconds * moveSpeed;
			float speedModifier = 1.0f;
			if(m_GroundSpeedProperty != null)
			{
				speedModifier = (m_IsRunning ? 1.0f - m_GroundSpeedProperty.Value : m_GroundSpeedProperty.Value)*0.5f+0.5f;
			}
			m_Actor.gameObject.transform.Translate(new Vector3(1.0f, 0.0f, 0.0f) * m_HorizontalSpeed * speedModifier * elapsedSeconds * moveSpeed);
			if(m_Walk != null && m_Run != null)
			{
				if(m_HorizontalSpeed == 0.0f && m_WalkMix == 0.0f && m_RunMix == 0.0f)
				{
					m_Walk.Time = 0.0f;
					//m_WalkTime = 0.0f;
					m_RunTime = 0.0f;
				}
				else
				{
					//m_WalkTime = m_WalkTime + elapsedSeconds * 0.9f * (m_HorizontalSpeed > 0 ? 1.0f : -1.0f) * (scaleX < 0.0 ? -1.0f : 1.0f);
					m_Walk.Advance(elapsedSeconds * 0.9f * (m_HorizontalSpeed > 0 ? 1.0f : -1.0f) * (scaleX < 0.0 ? -1.0f : 1.0f));
					// Sync up the run and walk times.
					//m_WalkTime %= m_Walk.Duration;
					/*if(m_WalkTime < 0.0f)
					{
						m_WalkTime += m_Walk.Duration;
					}*/
					m_RunTime = (m_Walk.Time-m_Walk.MinTime) / m_Walk.MaxTime * m_Run.Duration;
				}

				if(m_WalkMix != 0.0f)
				{
					m_Walk.Apply(m_WalkMix);
				}
				if(m_RunMix != 0.0f)
				{
					m_Run.Apply(m_RunTime, actorInstance, m_RunMix);
				}


				if(m_WalkToIdle != null && m_HorizontalSpeed == 0.0f && m_WalkToIdleTime < m_WalkToIdle.Duration)
				{
					m_WalkToIdleTime += elapsedSeconds;
					m_WalkToIdle.Apply(m_WalkToIdleTime, actorInstance, Math.Min(1.0f, m_WalkToIdleTime/m_WalkToIdle.Duration));
					//m_RunMix = m_WalkMix = 0.0;
				}
			}

			if(m_Aim != null)
			{
				// Figure out best aim position.
				Vec2D actorTarget = new Vec2D(actorLocalCursor[0], actorLocalCursor[1]);

				// Now actorTarget is in Nima root space.
				float maxDot = -1.0f;
				int bestIndex = 0;
				AimSlice[] lookup = m_HorizontalSpeed == 0.0f ? m_AimLookup : m_AimWalkingLookup;
				
				Vec2D targetDir = new Vec2D();
				for(int i = 0; i < AimSliceCount; i++)
				{
					AimSlice aim = lookup[i];

					
					Vec2D.Subtract(targetDir, actorTarget, aim.point);
					Vec2D.Normalize(targetDir, targetDir);
					float d = Vec2D.Dot(targetDir, aim.dir);
					if(d > maxDot)
					{
						maxDot = d;
						bestIndex = i;
					}
				}
				float targetAimTime = bestIndex/(float)(AimSliceCount-1) * m_Aim.Duration;

				m_AimAnimationTime += (targetAimTime-m_AimAnimationTime) * Math.Min(1.0f, elapsedSeconds*10.0f);
				m_Aim.Apply(m_AimAnimationTime, m_Actor.ActorInstance, 1.0f);
			}
		}
	}
}