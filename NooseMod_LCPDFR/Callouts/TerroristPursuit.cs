﻿//    NooseMod LCPDFR Plugin with Database System
//    Terrorist Pursuit Callout: called when the player is not a SWAT member
//    Copyright (C) 2017 Naruto 607

//    This program is free software: you can redistribute it and/or modify
//    it under the terms of the GNU General Public License as published by
//    the Free Software Foundation, either version 3 of the License, or
//    (at your option) any later version.

//    This program is distributed in the hope that it will be useful,
//    but WITHOUT ANY WARRANTY; without even the implied warranty of
//    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//    GNU General Public License for more details.

//    You should have received a copy of the GNU General Public License
//    along with this program.  If not, see <http://www.gnu.org/licenses/>.

//    Greetings to Sam @ LCPDFR.com for this wonderful API feature.

using LCPD_First_Response.Engine.Scripting.Entities;
using NooseMod_LCPDFR.Properties;
using NooseMod_LCPDFR.Mission_Controller;
using NooseMod_LCPDFR.Global_Controller;
using System;

namespace NooseMod_LCPDFR.Callouts
{
    #region Uses
    using System.Linq;

    using GTA;

    using LCPD_First_Response.Engine;
    using LCPD_First_Response.LCPDFR.API;
    using LCPD_First_Response.LCPDFR.Callouts;
    #endregion

    /// <summary>
    /// This is loaded when not played as a NOOSE/SWAT member (such as regular cop or FIB).
    /// NOOSE may require your assistance when one or two terrorists break from the crime scene.
    /// Beware, they can't take kindly to law enforcers, so a little chance is that they will be packed with a rocket launcher.
    /// </summary>
    //[CalloutInfo("Pursuit", ECalloutProbability.VeryLow)]
    [CalloutInfo("TerroristPursuit", ECalloutProbability.VeryLow)]
    internal class TerroristPursuit : Callout
    {
        /// <summary>
        /// Criminal models that can be used.
        /// </summary>
        //private string[] criminalModels = { "M_Y_THIEF", "M_Y_THIEF", "M_Y_GRUS_LO_01", "M_Y_GRU2_LO_01", "M_Y_GMAF_LO_01", "M_Y_GMAF_HI_01", "M_Y_GTRI_LO_01", "M_Y_GTRI_LO_02", "M_Y_GALB_LO_01", "M_Y_GALB_LO_02" };
        private string[] criminalModels = SettingsFile.Open("LCPDFR\\Plugins\\NooseMod.ini").
            GetValueString("CriminalModel", "WorldSettings", "M_M_GUNNUT_01;").
            Split(new char[] { ';' });

        /// <summary>
        /// Vehicle models that can be used.
        /// </summary>
        private string[] vehicleModels = { "NSTOCKADE", "NOOSE", "POLICE", "POLICE2", "SPEEDO", "TAXI", "LANDSTALKER", "ORACLE", "TAXI2", "CAVALCADE", "AMBULANCE" };

        /// <summary>
        /// Vehicle models that can be used.
        /// </summary>
        private string[] fastNooseVehicles = { "NOOSE", "POLICE", "POLICE2", "POLPATRIOT" };

        /// <summary>
        /// The pursuit.
        /// </summary>
        private LHandle pursuit;

        /// <summary>
        /// The terrorists fleeing the crime scene.
        /// </summary>
        private LPed[] terrorists;

        /// <summary>
        /// The vehicle.
        /// </summary>
        private LVehicle vehicle;

        /// <summary>
        /// The position at which the vehicles are spawned
        /// </summary>
        private Vector3 spawnPosition;

        #region Beta Player Cash Gain
        // As far as I concern, only for Arrested ones can get a cash, not with the killed ones

        /// <summary>
        /// How much money gained for killed suspects (terrorists)
        /// </summary>
        internal int cashForKilledTerrors = 200; // Cops participated only get 2/3 of the normal

        /// <summary>
        /// How much money gained for suspects (terrorists) that are arrested
        /// </summary>
        internal int cashForArrestedTerrors = 500; // Cops participated only get 2/3 of the normal
        #endregion

        /// <summary>
        /// Initializes a new instance of the <see cref="TerroristPursuit"/> class.
        /// </summary>
        public TerroristPursuit()
        {
            // Get a good position
            this.spawnPosition = World.GetNextPositionOnStreet(LPlayer.LocalPlayer.Ped.Position.Around(400.0f));

            while (this.spawnPosition.DistanceTo(LPlayer.LocalPlayer.Ped.Position) < 100.0f)
            {
                this.spawnPosition = World.GetNextPositionOnStreet(LPlayer.LocalPlayer.Ped.Position.Around(400.0f));
            }

            if (this.spawnPosition == Vector3.Zero)
            {
                // It obviously failed, set the position to be the player's position and the distance check will catch it.
                this.spawnPosition = LPlayer.LocalPlayer.Ped.Position;
            }

            // Show user where the pursuit is about to happen
            this.ShowCalloutAreaBlipBeforeAccepting(this.spawnPosition, 50f);
            this.AddMinimumDistanceCheck(80f, this.spawnPosition);

            // Set up message
            //this.CalloutMessage = string.Format(Functions.GetStringFromLanguageFile(Resources.CALLOUT_NOOSEMOD_POLICE_BACKUP_REQUIRED), Functions.GetAreaStringFromPosition(this.spawnPosition));
            this.CalloutMessage = string.Format(Resources.CALLOUT_NOOSEMOD_POLICE_BACKUP_REQUIRED, Functions.GetAreaStringFromPosition(this.spawnPosition));
            int rand = Common.GetRandomValue(0, 3);
            switch (rand)
            {
                case 0:
                    Functions.PlaySoundUsingPosition(ESound.PursuitAcknowledged, this.spawnPosition); break;
                case 1:
                    Functions.PlaySoundUsingPosition("INS_THIS_IS_CONTROL_I_NEED_ASSISTANCE_FOR CRIM_AN_OFFICER_IN_NEED_OF_ASSISTANCE IN_OR_ON_POSITION", this.spawnPosition); break;
                case 2:
                    Functions.PlaySoundUsingPosition("INS_THIS_IS_CONTROL_WE_HAVE INS_TRAFFIC_ALERT_FOR CRIM_AN_OFFICER_IN_NEED_OF_ASSISTANCE IN_OR_ON_POSITION", this.spawnPosition); break;
            }
        }


        /// <summary>
        /// Called when the callout has been accepted. Call base to set state to Running.
        /// </summary>
        /// <returns>
        /// True if callout was setup properly, false if it failed. Calls <see cref="End"/> when failed.
        /// </returns>
        public override bool OnCalloutAccepted()
        {
            bool pursuitReady = base.OnCalloutAccepted();

            try
            {
                // Create pursuit instance
                this.pursuit = Functions.CreatePursuit();

                // Create 
                this.vehicle = new LVehicle(World.GetNextPositionOnStreet(this.spawnPosition), Common.GetRandomCollectionValue<string>(this.vehicleModels));
                //if (this.vehicle.Exists())
                if (ValidityCheck.isObjectValid(this.vehicle))
                {
                    // Ensure vehicle is freed on end
                    Functions.AddToScriptDeletionList(this.vehicle, this);
                    this.vehicle.PlaceOnNextStreetProperly();
                    //this.vehicle.EngineRunning = this.vehicle.Exists();
                    this.vehicle.EngineRunning = ValidityCheck.isObjectValid(this.vehicle);

                    int peds = Common.GetRandomValue(1, 4);

                    // Create suspects
                    this.terrorists = new LPed[peds];
                    for (int i = 0; i < this.terrorists.Length; i++)
                    {
                        // Spawn ped
                        this.terrorists[i] = new LPed(World.GetNextPositionOnStreet(this.vehicle.Position), Common.GetRandomCollectionValue<string>(this.criminalModels), LPed.EPedGroup.Criminal);
                        //if (this.terrorists[i].Exists())
                        if (ValidityCheck.isObjectValid(this.terrorists[i]))
                        {
                            // If vehicle doesn't have a driver yet, warp terrorist as driver
                            if (!this.vehicle.HasDriver)
                            {
                                this.terrorists[i].WarpIntoVehicle(this.vehicle, VehicleSeat.Driver);
                            }
                            else
                            {
                                this.terrorists[i].WarpIntoVehicle(this.vehicle, VehicleSeat.AnyPassengerSeat);
                                this.terrorists[i].WillDoDrivebys = true;
                            }

                            // Make ignore all events and give specified weapons
                            this.terrorists[i].BlockPermanentEvents = true;
                            this.terrorists[i].Task.AlwaysKeepTask = true;
                            this.terrorists[i].EquipWeapon();

                            this.terrorists[i].Weapons.RemoveAll();
                            bool acquireRPG = Common.GetRandomBool(0, 7, 1);
                            if (acquireRPG)
                            {
                                // 1 by 6 chance a terrorist gets a rocket launcher
                                this.terrorists[i].Weapons.FromType(Weapon.Handgun_Glock).Ammo = 999;
                                this.terrorists[i].Weapons.FromType(Weapon.Heavy_RocketLauncher).Ammo = 15;
                                this.terrorists[i].Weapons.Select(Weapon.Heavy_RocketLauncher);
                                Functions.AddTextToTextwall("Be advised, one of the suspects are carrying a rocket launcher. Caution is advised.",
                                    Functions.GetStringFromLanguageFile("POLICE_SCANNER_CONTROL"));
                            }
                            else
                            {
                                // If false, AK-47 is used
                                this.terrorists[i].Weapons.FromType(Weapon.Rifle_AK47).Ammo = 2 ^ 13;
                                this.terrorists[i].Weapons.FromType(Weapon.Handgun_Glock).Ammo = 999;
                                this.terrorists[i].Weapons.Select(Weapon.Rifle_AK47);
                            }

                            // Add to deletion list and to pursuit
                            Functions.AddToScriptDeletionList(this.terrorists[i], this);
                            Functions.AddPedToPursuit(this.pursuit, this.terrorists[i]);
                        }
                    }
                    this.vehicle.Speed = (float)Common.GetRandomValue(20, 45);

                    // Create NOOSE personnel in a fast NOOSE car
                    LVehicle copCar = new LVehicle(World.GetNextPositionOnStreet(this.vehicle.Position), Common.GetRandomCollectionValue<string>(this.fastNooseVehicles));
                    if (copCar.Exists())
                    {
                        Functions.AddToScriptDeletionList(copCar, this);
                        copCar.PlaceOnNextStreetProperly();

                        LPed[] Nooses =
                        {
                            copCar.CreatePedOnSeat(VehicleSeat.Driver, new CModel(new Model("M_Y_SWAT")), RelationshipGroup.Cop),
                            copCar.CreatePedOnSeat(VehicleSeat.RightFront, new CModel(new Model("M_Y_SWAT")), RelationshipGroup.Cop),
                            copCar.CreatePedOnSeat(VehicleSeat.LeftRear, new CModel(new Model("M_Y_SWAT")), RelationshipGroup.Cop),
                            copCar.CreatePedOnSeat(VehicleSeat.RightRear, new CModel(new Model("M_Y_SWAT")), RelationshipGroup.Cop)
                        };

                        for (int i = 0; i < Nooses.Length; i++)
                        {
                            //if (Nooses[i] != null && Nooses[i].Exists())
                            if (ValidityCheck.isObjectValid(Nooses[i]))
                            {
                                Functions.AddToScriptDeletionList(Nooses[i], this);
                                copCar.SirenActive = true;
                            }
                        }
                        copCar.Speed = (float)Common.GetRandomValue(10, 45);
                        copCar.EngineRunning = copCar.Exists();
                    }

                    // Since we want other cops to join, set as called in already and also active it for player
                    Functions.SetPursuitCalledIn(this.pursuit, true);
                    Functions.SetPursuitIsActiveForPlayer(this.pursuit, true);
                    Functions.SetPursuitForceSuspectsToFight(this.pursuit, true);

                    // Show message to the player
                    Functions.PrintText(Functions.GetStringFromLanguageFile("CALLOUT_ROBBERY_CATCH_UP"), 25000);
                }
                pursuitReady = true;
            }
            catch (Exception ex) { Log.Error("Cannot create Pursuit instance: " + ex, this); pursuitReady = false; }
            return pursuitReady;
        }

        /// <summary>
        /// Called every tick to process all script logic. Call base when overriding.
        /// </summary>
        public override void Process()
        {
            base.Process();

            int arrestCount = this.terrorists.Count(terrorist => terrorist.Exists() && terrorist.HasBeenArrested);
            //int killCount = this.terrorists.Count(terrorist => terrorist.Exists() && terrorist.IsDead);

            // Merge kill and arrest into one, then print results when done
            if (arrestCount /*+ killCount*/ == this.terrorists.Length)
            {
                int totalCashGained = (cashForArrestedTerrors * arrestCount) /*+ (cashForKilledTerrors * killCount)*/;
                Functions.PrintText("Excellent work! You have gained $" + totalCashGained.ToString() + " for your service.", 10000);
                LPlayer.LocalPlayer.Money += totalCashGained;
                SetCalloutFinished(true, true, false);
                this.End();
            }

            // End this script is pursuit is no longer running, e.g. because all suspects are dead
            if (!Functions.IsPursuitStillRunning(this.pursuit))
            {
                /*// You'll get nothing if cashGained is 0
                int cashGained = (cashForArrestedTerrors * arrestCount) + (cashForKilledTerrors * killCount);
                if (cashGained == 0)
                {
                    Functions.PrintText("Few suspects are at large. We'll get them next time. +$" + cashGained + " for your service.", 10000);
                    LPlayer.LocalPlayer.Money += cashGained;
                }*/
                this.SetCalloutFinished(true, true, true);
                this.End();
            }
        }

        /// <summary>
        /// Put all resource free logic here. This is either called by the calloutmanager to shutdown the callout or can be called by the 
        /// callout itself to execute the cleanup code. Call base to set state to None.
        /// </summary>
        public override void End()
        {
            base.End();

            // End pursuit if still running
            if (this.pursuit != null)
            {
                Functions.ForceEndPursuit(this.pursuit);
            }
        }

        /// <summary>
        /// Called when a ped assigned to the current script has left the script due to a more important action, such as being arrested by the player.
        /// This is invoked right before control is granted to the new script, so perform all necessary freeing actions right here.
        /// </summary>
        /// <param name="ped">The ped</param>
        public override void PedLeftScript(LPed ped)
        {
            base.PedLeftScript(ped);

            // Free ped
            Functions.RemoveFromDeletionList(ped, this);
            Functions.SetPedIsOwnedByScript(ped, this, false);
        }
    }
}