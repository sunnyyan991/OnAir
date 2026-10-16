using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace OnAir
{
    public enum MenuPage { Title, NewFlight, LoadGame, Credits, Running, Preparing, PrepareFailed, Sandbox }
    public enum LeaveTarget { None, Title, Quit }

    // Flow and input state only. Rendering is in MenuView; file safety stays in SaveController.
    public sealed class MenuController : MonoBehaviour
    {
        public GameEntry game;
        public MenuPage Page { get; private set; } = MenuPage.Title;
        public bool SettingsOpen { get; private set; }
        public bool DisplayOpen { get; private set; }
        public bool OverwriteOpen { get; private set; }
        public SaveSlot DeletingSlot { get; private set; }
        public bool DeleteOpen => DeletingSlot != null;
        public bool CanDeleteSelected => !busy&&(Page==MenuPage.NewFlight||Page==MenuPage.LoadGame)&&Selected>=0&&Selected<Slots.Count&&Slots[Selected].occupied&&game.saves.SavingEnabled;
        public LeaveTarget Leaving { get; private set; }
        public bool TitleQuitOpen { get; private set; }
        public string Notice { get; private set; } = "";
        public List<SaveSlot> Slots { get; private set; } = new List<SaveSlot>();
        public int Selected { get; private set; } = -1;
        // Country/region theme chosen for a new flight; loaded flights keep their saved theme.
        public int ThemeIndex { get; private set; }
        public ThemeRule SelectedTheme => game.journey.themes[Mathf.Clamp(ThemeIndex, 0, game.journey.themes.Length - 1)];
        public void CycleTheme() { if (!busy) ThemeIndex = (ThemeIndex + 1) % game.journey.themes.Length; }
        // Aircraft chosen for a new flight; loaded flights keep their saved aircraft.
        public int AircraftIndex { get; private set; }
        public AircraftModel SelectedAircraft { get { var models = game.flight.View.Models; return models.Count == 0 ? null : models[Mathf.Clamp(AircraftIndex, 0, models.Count - 1)]; } }
        public bool CanChooseAircraft => game.flight.View.Models.Count > 1;
        public void CycleAircraft() { if (!busy && CanChooseAircraft) AircraftIndex = (AircraftIndex + 1) % game.flight.View.Models.Count; }
        public bool ShowHud => Page == MenuPage.Running;
        public bool BlocksHud => !ShowHud || DisplayOpen || Leaving != LeaveTarget.None;
        SaveSlot staged;
        bool stagedNew, stagedConfirmed;
        bool busy;
        bool stagedReady;
        public string PreparationStatus { get; private set; } = "PREPARING FLIGHT";
        public bool Busy => busy;
        public void Initialize(GameEntry owner)
        {
            game=owner;
            game.session.menuPaused=true;game.saves.SuspendAutomatic=true;game.ShowTitleBackdrop();
        }
        public void OpenSlots(bool create)
        {
            if (busy || ShowHud || DeleteOpen || OverwriteOpen) return;
            Page = create ? MenuPage.NewFlight : MenuPage.LoadGame;Notice = "";Slots = game.saves.GetSlots();
            Selected = create ? Slots.FindIndex(s => !s.occupied && !s.IsLegacy) : Slots.FindIndex(s => s.occupied);
        }
        public void Select(int index) { if (index >= 0 && index < Slots.Count && !busy&&!DeleteOpen&&!OverwriteOpen) { Selected = index;Notice = Slots[index].occupied&&!Slots[index].compatible?"OLD OR UNREADABLE SAVE. START NEW OR DELETE IT.":""; } }
        public void AskDeleteSelected()
        {
            if(!CanDeleteSelected||OverwriteOpen||DisplayOpen)return;
            DeletingSlot=Slots[Selected];Notice="";
        }
        public bool ConfirmDelete()
        {
            if(!DeleteOpen||busy)return false;
            int index=DeletingSlot.index,selection=Selected;
            bool deleted=game.saves.DeleteSlot(index);
            Notice=game.saves.Status;Slots=game.saves.GetSlots();
            if(deleted)
            {
                DeletingSlot=null;Selected=Slots.FindIndex(slot=>slot.index==index);
                if(Selected<0)Selected=Slots.Count==0?-1:Mathf.Min(selection,Slots.Count-1);
            }
            return deleted;
        }
        public void StartSelected(bool confirmed = false)
        {
            if (busy || DeleteOpen || Selected < 0 || Selected >= Slots.Count) return;
            var slot = Slots[Selected];bool create = Page == MenuPage.NewFlight;
            if (create && slot.IsLegacy) return;
            if (!create && !slot.occupied) return;
            if (create && slot.occupied && !confirmed) { OverwriteOpen = true;return; }
            if (create) game.journey.SelectTheme(SelectedTheme);
            OverwriteOpen = false;staged = slot;stagedNew = create;stagedConfirmed = confirmed;stagedReady = false;StartCoroutine(Prepare());
        }
        IEnumerator Prepare()
        {
            busy = true;Page = MenuPage.Preparing;game.session.menuPaused = true;game.saves.SuspendAutomatic = true;Notice = "PREPARING FLIGHT...";
            PreparationStatus=stagedNew?"STARTING NEW FLIGHT":"READING SAVE";
            game.BeginFlightPreparation();
            // Draw the real cloud bank and loading UI before synchronous save/world work.
            yield return null;
            if (!stagedReady && !game.saves.StageSlot(staged,stagedNew,stagedConfirmed))
            { Notice = game.saves.Status;Page = stagedNew ? MenuPage.PrepareFailed : MenuPage.LoadGame;busy = false;if(!stagedNew)game.ShowTitleBackdrop();yield break; }
            if (stagedNew && SelectedAircraft != null) game.flight.View.SelectAircraft(SelectedAircraft.id);
            Exception failure = null;
            try { if (!stagedReady) { game.flight.Tick(0);game.cameraFollow.Follow(0);game.environment.Refresh(0,true);game.world.BeginPreparation();game.biomeNavigator.RefreshLabel(); } }
            catch (Exception error) { failure = error; }
            if (failure != null) { Debug.LogWarning(failure.Message);FailPreparation("COULD NOT PREPARE THIS FLIGHT");yield break; }
            PreparationStatus="BUILDING WORLD";
            float deadline = Time.unscaledTime + game.preparationTimeoutSeconds;
            while ((!game.world.ViewReady||!game.world.CanCaptureSave)&&game.world.PreparationError==null&&Time.unscaledTime<deadline)yield return null;
            if(game.world.PreparationError!=null){FailPreparation("COULD NOT PREPARE THIS FLIGHT");yield break;}
            if (!game.world.ViewReady||!game.world.CanCaptureSave) { Debug.LogWarning("Flight preparation timed out waiting for: "+game.world.PreparationWaitingOn);game.world.CancelPreparation();FailPreparation("FLIGHT PREPARATION TIMED OUT");yield break; }
            stagedReady = true;
            // Keep the completed progress bar visible for one rendered frame.
            // Batch-mode Editors do not render an end-of-frame callback.
            if(!Application.isBatchMode)yield return new WaitForEndOfFrame();
            yield return null;
            PreparationStatus=stagedNew?"SAVING FLIGHT":"READY";yield return null;
            if (!game.saves.CommitStagedSlot(stagedNew)) { FailPreparation("SAVE FAILED");yield break; }
            game.RevealFlight();Page = MenuPage.Running;SettingsOpen = DisplayOpen = false;Notice = "";game.session.menuPaused = false;busy = false;
        }
        void FailPreparation(string text) { if(game.world.PreparingView)game.world.CancelPreparation();else game.world.SuspendStreaming();Notice = text;Page = MenuPage.PrepareFailed;busy = false;game.saves.EndJourneyWithoutSaving(); }
        public void Retry() { if (Page == MenuPage.PrepareFailed && !busy) {if(game.airport.TestMode)StartCoroutine(PrepareAirportTest());else StartCoroutine(Prepare());} }
        public void Back()
        {
            if (busy) return;
            if (DeleteOpen) { DeletingSlot=null;Notice="";return; }
            if (OverwriteOpen) { OverwriteOpen = false;return; }
            if (DisplayOpen) { game.display.RevertMode();DisplayOpen = false;return; }
            Page = MenuPage.Title;Notice = "";SettingsOpen = false;game.saves.EndJourneyWithoutSaving();game.session.menuPaused = true;
            game.ShowTitleBackdrop();
        }
        public void ShowCredits() { if (Page == MenuPage.Title) Page = MenuPage.Credits; }
        public void OpenSandbox(){if(Page!=MenuPage.Title||busy)return;game.citySandbox.Open();Page=MenuPage.Sandbox;}
        public void StartAirportTest(){if(Page==MenuPage.Title&&!busy)StartCoroutine(PrepareAirportTest());}
        public void NextAirportTest(){if(ShowHud&&game.airport.TestMode&&!busy)StartCoroutine(PrepareAirportTest(true));}
        IEnumerator PrepareAirportTest(bool next=false)
        {
            busy=true;Page=MenuPage.Preparing;game.session.menuPaused=true;
            game.saves.EndJourneyWithoutSaving();game.airport.Reset(true);
            game.BeginFlightPreparation();yield return null;
            Exception failure=null;
            try
            {
                game.world.RestoreState(null);if(!next)game.journey.Restart(2409);game.session.paused=false;
                if(!game.airport.JumpToTest())throw new InvalidOperationException("Airport asset is not ready");
                game.cameraFollow.Follow(0);game.environment.Refresh(0,true);game.world.BeginPreparation();
            }
            catch(Exception error){failure=error;}
            if(failure!=null){Debug.LogWarning(failure.Message);FailPreparation("AIRPORT TEST PREPARATION FAILED");yield break;}
            PreparationStatus="PREPARING AIRPORT TEST";
            float deadline=Time.unscaledTime+game.preparationTimeoutSeconds;
            while(!game.world.ViewReady&&game.world.PreparationError==null&&Time.unscaledTime<deadline)yield return null;
            if(!game.world.ViewReady){FailPreparation("AIRPORT TEST PREPARATION FAILED");yield break;}
            yield return null;game.RevealFlight();Page=MenuPage.Running;SettingsOpen=DisplayOpen=false;
            Notice="AIRPORT TEST - SAVES UNCHANGED";game.session.menuPaused=false;busy=false;
        }
        public void ToggleSettings()
        {
            if (!ShowHud || BlocksHud) return;
            SettingsOpen = !SettingsOpen;game.panel.CloseBiomePicker();
        }
        public void OpenDisplay() { if (!busy) DisplayOpen = true; }
        public bool SaveGame()
        {
            if (!ShowHud) return false;
            bool result = game.saves.SaveNow();Notice = result ? "GAME SAVED" : "SAVE FAILED";return result;
        }
        public void AskLeave(LeaveTarget target)
        {
            if (!ShowHud) { if (target == LeaveTarget.Quit && Page == MenuPage.Title) TitleQuitOpen = true;return; }
            Leaving = target;game.saves.SuspendAutomatic = true;game.saves.FinishPendingWrite();game.session.menuPaused = true;Notice = "";
            game.panel.ReleaseInput();
            if (!game.saves.HasUnsavedChanges) CompleteLeave(false);
        }
        public void CancelLeave()
        {
            Leaving = LeaveTarget.None;TitleQuitOpen = false;OverwriteOpen = false;DeletingSlot=null;Notice = "";
            if (ShowHud) { game.session.menuPaused = false;game.saves.SuspendAutomatic = false; }
        }
        public bool CompleteLeave(bool save)
        {
            if (Leaving == LeaveTarget.None) return false;
            if (save && !SaveGame()) return false;
            var target = Leaving;game.saves.EndJourneyWithoutSaving();Leaving = LeaveTarget.None;SettingsOpen = DisplayOpen = false;
            game.session.menuPaused = true;
            if (target == LeaveTarget.Title) { Page = MenuPage.Title;Notice="";game.ShowTitleBackdrop(); }
            else Quit();
            return true;
        }
        public void Quit()
        {
            game.saves.EndJourneyWithoutSaving();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
        public void Escape()
        {
            if (game.display.ModePending) game.display.RevertMode();
            else if (Leaving != LeaveTarget.None || TitleQuitOpen || OverwriteOpen || DeleteOpen) CancelLeave();
            else if (DisplayOpen) Back();
            else if (game.panel.IsBiomePickerOpen) game.panel.CloseBiomePicker();
            else if (SettingsOpen) SettingsOpen = false;
            else if (Page != MenuPage.Title && Page != MenuPage.Running) Back();
        }
        public bool CoversHudPoint(Vector2 point)
        {
            if (BlocksHud) return true;
            return new Rect(20,170,270,SettingsOpen ? 280 : 38).Contains(point);
        }
    }
}
