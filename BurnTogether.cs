using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace BurnTogether
{
	public class BurnTogether : PartModule
	{
		public bool isLeader = false;
		public bool isFollowing = false;
		public bool hasLeader = false;
		public bool throttleLimit = false;
		public float throttleLimitFactor = 1;
		public bool roverMode = false;
		public bool atmosphericMode = false;


		List<BurnTogether> warpLockedFollowers = new List<BurnTogether>();
		public List<BurnTogether> followers = new List<BurnTogether>();
		
		
		private float throttleRatio;
		private BurnTogether leader;
		private AnimationState[] indicatorStates;
		private bool beginWarp = true;
		private Vector3d relPosition;
		
		[KSPField(isPersistant = false, guiActive = true, guiName = "Status")]
        public string statusGui = "Off";
		[KSPField(isPersistant = false, guiActive = true, guiName = "AG Mimic")]
		public bool mimicAG = false;
		
		
		public LineRenderer foof; //debug
        
		#region GUIButtons
		//===============GUI Buttons===================
		[KSPEvent(guiActive = true, guiName = "Set as Leader")]
		public void SetAsLeader()
		{
			SetOff ();
			//Events["ToggleAGM"].active = true;
			isLeader = true;
			ScreenMessages.PostScreenMessage(this.vessel.vesselName+" set as leader", 5, ScreenMessageStyle.UPPER_CENTER);
			statusGui = "Leading";
			
			this.vessel.OnFlyByWire += new FlightInputCallback(LimitLeaderThrottle);
			
			if(indicatorStates.Length > 0)
			{
				foreach(AnimationState anim in indicatorStates)
				{
					anim.normalizedTime = 0.5f;
				}
			}
		}
		
		
		[KSPEvent(guiActive = true, guiName = "Set as Follower")]
		public void SetAsFollower()
		{
			SetOff ();
			isFollowing = true;
			this.vessel.ActionGroups.groups[3] = true; //enable rcs
			this.vessel.ActionGroups.groups[4] = true; //enable sas

			foreach(Vessel v in FlightGlobals.Vessels)
			{
				if(!v.packed)
				{
					Debug.Log ("Checking if leader: "+v.vesselName);
					foreach(BurnTogether pp in v.FindPartModulesImplementing<BurnTogether>())
					{
						//Debug.Log ("Found BurnTogether module");
						if(pp.isLeader)
						{
							leader = pp;
							hasLeader = true;
							//Debug.Log ("Found Leader.");
							ScreenMessages.PostScreenMessage("Following "+leader.vessel.vesselName, 5, ScreenMessageStyle.UPPER_CENTER);
							statusGui = "Following "+leader.vessel.vesselName;
							if(indicatorStates.Length > 0)
							{
								foreach(AnimationState anim in indicatorStates)
								{
									anim.normalizedTime = 1;
								}
							}
							
							if(!pp.followers.Contains(this))
							{
								pp.followers.Add(this);
							}
							
							//RCS kill relative v code
							this.vessel.OnFlyByWire += new FlightInputCallback(RCSKillVelocity);
							
							break;
						}
					}
				}
			}
			

			if(!hasLeader)//if leader not found
			{
				Debug.Log ("Could not find leader.");
				ScreenMessages.PostScreenMessage("Could not find a leader.", 5, ScreenMessageStyle.UPPER_CENTER);
				SetOff ();
			}
		}
		
		
		[KSPEvent(guiActive = true, guiName = "All Follow Me")]
		public void AllFollow()
		{
			if(isFollowing)  //turn off other leader bt first
			{
				foreach(BurnTogether bt in leader.vessel.FindPartModulesImplementing<BurnTogether>())
				{
					bt.SetOff();
				}
			}
			
			SetAsLeader ();
			foreach(Vessel v in FlightGlobals.Vessels)
			{
				if(!v.packed)
				{
					foreach(BurnTogether pp in v.FindPartModulesImplementing<BurnTogether>())
					{
						if(!v.Equals(this.vessel))
						{
							pp.SetAsFollower();
							ScreenMessages.PostScreenMessage("Acquiring Follower: "+v.vesselName);
						}
					}
				}
			}
		}
		
		
		[KSPEvent(guiActive = true, guiName = "BT Off")]
		public void SetOff()
		{
			if(indicatorStates.Length > 0)
			{
				foreach(AnimationState anim in indicatorStates)
				{
					anim.normalizedTime = 0;
				}
			}
			if(isFollowing)
			{
				if(leader)
				{
					leader.followers.Remove(this);
				}

				this.vessel.OnFlyByWire -= new FlightInputCallback(RCSKillVelocity);
				if(roverMode)
				{
					this.vessel.OnFlyByWire -= new FlightInputCallback(RoverControl);
					roverMode = false;
				}
				if(atmosphericMode)
				{
					Debug.Log ("atmosphericMode disabled");
					atmosphericMode = false;
				}

				//reset sas
				vessel.Autopilot.SAS.DisconnectFlyByWire();
				vessel.Autopilot.SAS.ConnectFlyByWire();
			}
			if(isLeader)
			{
				ScreenMessages.PostScreenMessage("Releasing Followers", 5, ScreenMessageStyle.UPPER_CENTER);
			
				this.vessel.OnFlyByWire -= new FlightInputCallback(LimitLeaderThrottle);
	

				foreach(BurnTogether fBt in followers)
				{
					fBt.SetOff();
					ScreenMessages.PostScreenMessage("Releasing Follower: "+fBt.vessel.vesselName);
				}

				followers.Clear();
			}
			//Events["ToggleAGM"].active = false;
			mimicAG = false;
			isLeader = false;
			isFollowing = false;
			hasLeader = false;
			leader = null;
			statusGui = "Off";
			throttleLimitFactor = 1;
		}
		
		
		[KSPEvent (guiActive = true, guiName = "Toggle AG Mimic", active = true)]
		public void ToggleAGM()
		{
			mimicAG = !mimicAG;	
		}
		#endregion

		#region ActionGroups
		//=============Action Groups===================
		
		[KSPAction("Set as Leader")]
		public void AGSetAsLeader(KSPActionParam param)
		{
			SetAsLeader();
		}
		
		[KSPAction("Set as Follower")]
		public void AGSetAsFollower(KSPActionParam param)
		{
			SetAsFollower();
		}
		
		[KSPAction("All Follow Me")]
		public void AGAllFollowMe(KSPActionParam param)
		{
			AllFollow();
		}
		
		[KSPAction("Off")]
		public void AGOff(KSPActionParam param)
		{
			SetOff();
		}
		
		
		
		[KSPAction("AG1 Command", KSPActionGroup.Custom01)]
		public void FireAG1(KSPActionParam param)
		{
			if(isLeader && mimicAG)
			{
				foreach(BurnTogether fBt in followers)
				{
					fBt.vessel.ActionGroups.ToggleGroup(KSPActionGroup.Custom01);	
				}
			}
		}
		
		
		[KSPAction("AG2 Command", KSPActionGroup.Custom02)]
		public void FireAG2(KSPActionParam param)
		{
			if(isLeader && mimicAG)
			{
				foreach(BurnTogether bt in followers)
				{
					if(bt.isFollowing)
					{
						bt.vessel.ActionGroups.ToggleGroup(KSPActionGroup.Custom02);
					}
				}
			}
		}
		
		[KSPAction("AG3 Command", KSPActionGroup.Custom03)]
		public void FireAG3(KSPActionParam param)
		{
			if(isLeader && mimicAG)
			{
				foreach(BurnTogether bt in followers)
				{
					if(bt.isFollowing)
					{
						bt.vessel.ActionGroups.ToggleGroup(KSPActionGroup.Custom03);	
					}
				}

			}
		}
		
		[KSPAction("AG4 Command", KSPActionGroup.Custom04)]
		public void FireAG4(KSPActionParam param)
		{
			if(isLeader && mimicAG)
			{

				foreach(BurnTogether bt in followers)
				{
					if(bt.isFollowing)
					{
						bt.vessel.ActionGroups.ToggleGroup(KSPActionGroup.Custom04);	
					}
				}

			}
		}
		
		[KSPAction("AG5 Command", KSPActionGroup.Custom05)]
		public void FireAG5(KSPActionParam param)
		{
			if(isLeader && mimicAG)
			{
				foreach(BurnTogether bt in followers)
				{
					if(bt.isFollowing)
					{
						bt.vessel.ActionGroups.ToggleGroup(KSPActionGroup.Custom05);	
					}
				}
			}
		}
		
		[KSPAction("AG6 Command", KSPActionGroup.Custom06)]
		public void FireAG6(KSPActionParam param)
		{
			if(isLeader && mimicAG)
			{
				foreach(BurnTogether bt in followers)
				{
					if(bt.isFollowing)
					{
						bt.vessel.ActionGroups.ToggleGroup(KSPActionGroup.Custom06);	
					}
				}
			}
		}
		
		[KSPAction("AG7 Command", KSPActionGroup.Custom07)]
		public void FireAG7(KSPActionParam param)
		{
			if(isLeader && mimicAG)
			{
				foreach(BurnTogether bt in followers)
				{
					if(bt.isFollowing)
					{
						bt.vessel.ActionGroups.ToggleGroup(KSPActionGroup.Custom07);	
					}
				}
			}
		}
		
		[KSPAction("AG8 Command", KSPActionGroup.Custom08)]
		public void FireAG8(KSPActionParam param)
		{
			if(isLeader && mimicAG)
			{
				foreach(BurnTogether bt in followers)
				{
					if(bt.isFollowing)
					{
						bt.vessel.ActionGroups.ToggleGroup(KSPActionGroup.Custom08);	
					}
				}
			}
		}
		
		[KSPAction("AG9 Command", KSPActionGroup.Custom09)]
		public void FireAG9(KSPActionParam param)
		{
			if(isLeader && mimicAG)
			{
				foreach(BurnTogether bt in followers)
				{
					if(bt.isFollowing)
					{
						bt.vessel.ActionGroups.ToggleGroup(KSPActionGroup.Custom09);	
					}
				}
			}
		}
		
		[KSPAction("AG10 Command", KSPActionGroup.Custom10)]
		public void FireAG10(KSPActionParam param)
		{
			if(isLeader && mimicAG)
			{
				foreach(BurnTogether bt in followers)
				{
					if(bt.isFollowing)
					{
						bt.vessel.ActionGroups.ToggleGroup(KSPActionGroup.Custom10);	
					}
				}
			}
		}
		
		[KSPAction("Abort Command", KSPActionGroup.Abort)]
		public void FireAbort(KSPActionParam param)
		{
			if(isLeader && mimicAG)
			{
				foreach(BurnTogether bt in followers)
				{
					if(bt.isFollowing)
					{
						bt.vessel.ActionGroups.ToggleGroup(KSPActionGroup.Abort);	
					}
				}
			}
		}
		
		
		#endregion
		
		//===========PartModule Overrides==============
		
		
		public override void OnStart(PartModule.StartState state)
		{
			//Debug.Log("BT Start");
			
			indicatorStates = Utils.SetUpAnimation ("indicatorLight", this.part);
			
			
			SetOff ();
			
			/*
			foof = gameObject.AddComponent<LineRenderer>(); //debug
			foof.SetWidth(0.5f, 0.5f);
			foof.SetVertexCount(2);
			*/
			
			part.OnJustAboutToBeDestroyed += new Callback(SetOff);
			
		}
		
		
		public override void OnUpdate()
		{
			
			if(!this.vessel.IsControllable) //turn off when vessel is uncontrollable
			{
				SetOff ();
			}
			

			if(isLeader)
			{
				//leader warp handling
				if(TimeWarp.CurrentRate>1 && TimeWarp.WarpMode == TimeWarp.Modes.HIGH)
				{
					if(beginWarp)
					{
						beginWarp = false;
						Debug.Log ("Going into warp with "+followers.Count+" followers");
						warpLockedFollowers.Clear();
						foreach(BurnTogether bt in followers)
						{
							if((bt.vessel.obt_velocity-vessel.obt_velocity).sqrMagnitude < 0.01f)
							{
								warpLockedFollowers.Add(bt);
								bt.relPosition = bt.vessel.transform.position - vessel.transform.position;
							}
						}
						Debug.Log (warpLockedFollowers.Count+" warp-locked followers.");
					}
					else
					{
						foreach(BurnTogether wBt in warpLockedFollowers)
						{

							wBt.vessel.SetPosition(vessel.transform.position+wBt.relPosition);
							//wBt.vessel.obt_velocity = this.vessel.obt_velocity;	
						}
					}
				}
				else
				{
					//ending warp. realign followers
					if(!beginWarp)
					{
						foreach(BurnTogether wBt in warpLockedFollowers)
						{
							wBt.vessel.SetPosition(vessel.transform.position+wBt.relPosition);
							wBt.vessel.obt_velocity = this.vessel.obt_velocity;	
						}
						warpLockedFollowers.Clear();
					}
					beginWarp = true;
				}
				//end leader warp handling
			}

			
			
			
			else if(isFollowing && hasLeader) //code for following leader
			{
				if((leader.vessel.GetWorldPos3D()-vessel.GetWorldPos3D()).sqrMagnitude > Vessel.unloadDistance*Vessel.unloadDistance)
				{
					SetOff ();
				}

				
				foreach(BurnTogether bt in leader.vessel.FindPartModulesImplementing<BurnTogether>())  //bug if there is more than one BT module?
				{
					mimicAG = bt.mimicAG;	//activate/deactivate mimicAG on followers
				}
				
				throttleRatio = 0;
				throttleRatio += (GetThrustToWeight(leader.vessel)/GetThrustToWeight(this.vessel));
				
				
				if(vessel.checkLanded())  //===================================================================Rover Mode===============
				{
					if(!roverMode)
					{
						Debug.Log ("roverMode enabled");
						roverMode = true;
						this.vessel.OnFlyByWire += new FlightInputCallback(RoverControl);
						if(atmosphericMode)
						{
							Debug.Log ("atmosphericMode disabled");
							atmosphericMode = false;
						}
					}
				}
				else
				{
					if(roverMode)
					{
						Debug.Log ("roverMode disabled");
						roverMode = false;
						this.vessel.OnFlyByWire -= new FlightInputCallback(RoverControl);
					}
				}

				/*
				if(this.vessel.atmDensity>0)
				{
					if(!atmosphericMode && !roverMode)
					{
						Debug.Log ("atmosphericMode enabled");
						atmosphericMode = true;
					}
				}
				else
				{
					if(atmosphericMode)
					{
						Debug.Log ("atmosphericMode disabled");
						atmosphericMode = false;
					}
				}
				*/
				
				if(leader.vessel.ctrlState.mainThrottle>0 && GetFinalThrustToWeight(leader.vessel)>0) //added check for leader fuel
				{
					float targetThrottle = Mathf.Clamp01 (leader.vessel.ctrlState.mainThrottle * throttleRatio);
					this.vessel.ctrlState.mainThrottle = targetThrottle;
				}
				else
				{
					this.vessel.ctrlState.mainThrottle = 0;
				}
				
				if(throttleRatio>1) //if followers have insufficient max thrust         //try removing this constraint and having throttle limit factor always updated, clamped to 1.
				{
					foreach(BurnTogether bt in leader.vessel.FindPartModulesImplementing<BurnTogether>())
					{
						if(((1/throttleRatio)-.001f) < bt.throttleLimitFactor)
						{
							bt.throttleLimitFactor = (1/throttleRatio)-.001f;
						}
						bt.throttleLimit = true;
					}
				}
				
				
				//steering --need to make more stable==============================================================STEERING
				Quaternion steeringTarget = leader.vessel.Autopilot.SAS.lockedHeading;

				
				if(atmosphericMode) //FUUUK
				{
					Vector3 killVector = leader.vessel.GetObtVelocity() - this.vessel.GetObtVelocity();
					Quaternion rotAdjust = Quaternion.Inverse (this.vessel.transform.rotation);
					/*
					foof.SetVertexCount(2);
					foof.SetPosition(0, vessel.transform.position);
					foof.SetPosition(1, vessel.transform.position + 20*(vessel.vesselTransform.right));// * Vector3.forward));
					*/
					killVector = rotAdjust * killVector;
					float pitchFactor = 1;
					//steeringTarget *= Quaternion.AngleAxis(Mathf.Clamp(pitchFactor * -killVector.z, -10, 10), -vessel.ReferenceTransform.right);
					
					//float yawFactor = 0.5f;
					//steeringTarget *= Quaternion.AngleAxis(Mathf.Clamp(yawFactor * killVector.x, -6, 6), vessel.ReferenceTransform.forward);
					
					//float rollFactor = 0.01f;
					//steeringTarget *= Quaternion.AngleAxis(Mathf.Clamp(rollFactor * -killVector.x, -1, 1), vessel.ReferenceTransform.forward);
					
					steeringTarget *= Quaternion.AngleAxis(Mathf.Clamp ((float)(leader.vessel.altitude-vessel.altitude)*0.7f, -18, 18), vessel.vesselTransform.right);
					//Debug.Log ("X: "+killVector.x+", Y: "+killVector.y+", Z: "+killVector.z);
					Debug.Log ("Pitch adjustment: "+pitchFactor * -killVector.z);
				}


				//set vessel's sas to steering target
				vessel.Autopilot.SetMode(VesselAutopilot.AutopilotMode.StabilityAssist);
				this.vessel.Autopilot.SAS.LockHeading(steeringTarget, true);
				this.vessel.Autopilot.Update();


				
				
				//action group mimic -- Togglables(gear, lights, rover brakes) handled here.  The rest are in the ActiongGroups section.
				if(mimicAG)
				{
					if(leader.vessel.ActionGroups.groups[1])
					{
						this.vessel.ActionGroups.SetGroup(KSPActionGroup.Gear, true);
					}
					else
					{
						this.vessel.ActionGroups.SetGroup(KSPActionGroup.Gear, false);
					}
					
					if(leader.vessel.ActionGroups.groups[2])
					{
						this.vessel.ActionGroups.SetGroup(KSPActionGroup.Light, true);
					}
					else
					{
						this.vessel.ActionGroups.SetGroup(KSPActionGroup.Light, false);
					}
				}
				if(mimicAG || roverMode)  //brake toggles mimiced only in rover mode.
				{
					if(leader.vessel.ActionGroups.groups[5])
					{
						this.vessel.ActionGroups.SetGroup(KSPActionGroup.Brakes, true);
					}
					else
					{
						this.vessel.ActionGroups.SetGroup(KSPActionGroup.Brakes, false);
					}
				}
			}
		}

		

		public override void OnInactive()
		{
			SetOff ();	
		}
		
		
		//=======Flight Inputs============
		
		public void RCSKillVelocity(FlightCtrlState s)
		{
			if(leader!=null && s!=null)
			{
				Vector3 killVector = leader.vessel.GetObtVelocity() - this.vessel.GetObtVelocity();
				Quaternion rotAdjust = Quaternion.Inverse (this.vessel.transform.rotation);
			
				killVector = rotAdjust * killVector;
				
				float rcsFactor;
				//rcsFactor = 1/(2*this.vessel.GetTotalMass());
				rcsFactor = 2;
				
				s.X = Mathf.Clamp (-rcsFactor*killVector.x, -1, 1);
				s.Y = Mathf.Clamp (-rcsFactor*killVector.z, -1, 1);
				s.Z = Mathf.Clamp (-rcsFactor*killVector.y, -1, 1);
			}
		}
			
		
		public void RoverControl(FlightCtrlState s)
		{
			Vector3 killVector = leader.vessel.GetObtVelocity() - this.vessel.GetObtVelocity();
			Quaternion rotAdjust = Quaternion.Inverse (this.vessel.transform.rotation);
			
			killVector = rotAdjust * killVector;
			
			float wheelThrottleFactor = 7;
			float wheelSteerFactor = 0.3f;
			
			if(Mathf.Abs (killVector.y) > Mathf.Abs (killVector.z))
			{
				s.wheelThrottle = Mathf.Clamp (wheelThrottleFactor*killVector.y, -1, 1);
			}
			else
			{
				s.wheelThrottle = Mathf.Clamp (wheelThrottleFactor*killVector.z, -1, 1);
			}
			s.wheelSteer = Mathf.Clamp(-wheelSteerFactor*killVector.x, -1, 1);
		}
		
		
		
		public void LimitLeaderThrottle(FlightCtrlState s)  //for cutting leader throttle if followers have insufficient thrust.
		{
			if(throttleLimit)
			{
				//Debug.Log ("Limiting leader throttle to "+throttleLimitFactor);
				if(this.vessel.ctrlState.mainThrottle>throttleLimitFactor)
				{
					s.mainThrottle = throttleLimitFactor;
				}
			}
		}
		
		
		
		
		//Utils
		
		private float GetThrustToWeight(Vessel v)
		{
			float totalThrust = 0;
			float vesselMass = v.GetTotalMass();
			foreach(Part p in v.parts)
			{
				foreach (ModuleEngines me in p.FindModulesImplementing<ModuleEngines>())
				{
					if(me.EngineIgnited)
					{
						totalThrust += (me.maxThrust * me.thrustPercentage * 100);
					}
				}
				
				foreach (ModuleEnginesFX me in p.FindModulesImplementing<ModuleEnginesFX>())
				{
					if(me.EngineIgnited)
					{
						totalThrust += (me.maxThrust * me.thrustPercentage * 100);
					}
				}
			}
			return totalThrust/vesselMass;
		}
		
		
		private float GetFinalThrustToWeight(Vessel v)
		{
			float totalThrust = 0;
			float vesselMass = v.GetTotalMass();
			foreach(Part p in v.parts)
			{
				foreach (ModuleEngines me in p.FindModulesImplementing<ModuleEngines>())
				{
					if(me.EngineIgnited)
					{
						totalThrust += me.finalThrust;
					}
				}
				foreach (ModuleEnginesFX me in p.FindModulesImplementing<ModuleEnginesFX>())
				{
					if(me.EngineIgnited)
					{
						totalThrust += me.finalThrust;
					}
				}
			}
			return totalThrust/vesselMass;
			
		}
		/*
		void OnGUI()
		{
			if(isLeader)
			{
				GUI.Label(new Rect(200,200,200,200), "angle of current rotation and locked heading: "+Quaternion.Angle(vessel.Autopilot.SAS.lockedHeading, vessel.Autopilot.SAS.currentRotation));
			}
		}
		*/
	}
}

