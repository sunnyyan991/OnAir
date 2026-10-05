using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace OnAir
{
    public enum MenuPage { Title, NewFlight, LoadGame, Credits, Running, Preparing, PrepareFailed }
    public enum LeaveTarget { None, Title, Quit }

    // Flow and input state only. Rendering is in MenuView; file safety stays in SaveController.
    public sealed class MenuController : MonoBehaviour
    {
        public GameEntry game;
        public MenuPage Page { get; private set; } = MenuPage.Title;
        public bool SettingsOpen { get; private set; }
        public bool DisplayOpen { get; private set; }
        public bool OverwriteOpen { get; private set; }
        public LeaveTarget Leaving { get; private set; }
        public bool TitleQuitOpen { get; private set; }
        public string Notice { get; private set; } = "";
        public List<SaveSlot> Slots { get; private set; } = new List<SaveSlot>();
        public int Selected { get; private set; } = -1;
        public bool ShowHud => Page == MenuPage.Running;
        public bool BlocksHud => !ShowHud || DisplayOpen || Leaving != LeaveTarget.None;
        SaveSlot staged;
        bool stagedNew, stagedConfirmed;
        bool busy;
        bool stagedReady;
        Coroutine backgroundRoutine;
        int sceneMask;
        public bool BackgroundReady { get; private set; }
        public bool BackgroundFromSave { get; private set; }
        public string BackgroundStatus { get; private set; } = "PREPARING BACKGROUND";
        public string PreparationStatus { get; private set; } = "PREPARING FLIGHT";
        public bool Busy => busy;
        public void Initialize(GameEntry owner)
        {
            game=owner;sceneMask=game.sceneCamera.cullingMask;
            game.session.menuPaused=true;game.saves.SuspendAutomatic=true;QueueBackground();
        }
        void ShowBackground(bool visible)
        {
            BackgroundReady=visible;game.sceneCamera.cullingMask=visible?sceneMask:0;
        }
        void StopBackground()
        {
            if(backgroundRoutine!=null)StopCoroutine(backgroundRoutine);
            backgroundRoutine=null;
            if(game.world.PreparingView)game.world.CancelPreparation();
        }
        void QueueBackground()
        {
            StopBackground();ShowBackground(false);BackgroundStatus="PREPARING BACKGROUND";
            backgroundRoutine=StartCoroutine(PrepareBackground());
        }
        IEnumerator PrepareBackground()
        {
            // Give the title a rendered frame before reading saves or preparing the city.
            yield return null;
            Exception failure=null;
            try
            {
                BackgroundFromSave=game.saves.PrepareTitlePreview(game.titleBackgroundSeed);
                game.flight.Tick(0);game.cameraFollow.Follow(0);game.environment.Refresh(0,true);
                game.world.BeginPreparation();game.biomeNavigator.RefreshLabel();
            }
            catch(Exception error){failure=error;}
            float deadline=Time.unscaledTime+game.preparationTimeoutSeconds;
            while(failure==null&&!game.world.ViewReady&&game.world.PreparationError==null&&Time.unscaledTime<deadline)yield return null;
            if(failure!=null||!game.world.ViewReady)
            {
                game.world.CancelPreparation();BackgroundStatus="BACKGROUND UNAVAILABLE";
                Debug.LogWarning("Title background: "+(failure?.Message??game.world.PreparationError??"preparation timed out"));
            }
            else {ShowBackground(true);BackgroundStatus=BackgroundFromSave?"LAST SAVED FLIGHT":"";}
            backgroundRoutine=null;
        }
        public void OpenSlots(bool create)
        {
            if (busy || ShowHud) return;
            Page = create ? MenuPage.NewFlight : MenuPage.LoadGame;Notice = "";Slots = game.saves.GetSlots();
            Selected = create ? Slots.FindIndex(s => !s.occupied && !s.IsLegacy) : Slots.FindIndex(s => s.occupied);
        }
        public void Select(int index) { if (index >= 0 && index < Slots.Count && !busy) { Selected = index;Notice = ""; } }
        public void StartSelected(bool confirmed = false)
        {
            if (busy || Selected < 0 || Selected >= Slots.Count) return;
            var slot = Slots[Selected];bool create = Page == MenuPage.NewFlight;
            if (create && slot.IsLegacy) return;
            if (!create && !slot.occupied) return;
            if (create && slot.occupied && !confirmed) { OverwriteOpen = true;return; }
            StopBackground();ShowBackground(false);
            OverwriteOpen = false;staged = slot;stagedNew = create;stagedConfirmed = confirmed;stagedReady = false;StartCoroutine(Prepare());
        }
        IEnumerator Prepare()
        {
            busy = true;Page = MenuPage.Preparing;game.session.menuPaused = true;game.saves.SuspendAutomatic = true;Notice = "PREPARING FLIGHT...";
            PreparationStatus=stagedNew?"STARTING NEW FLIGHT":"READING SAVE";
            yield return null;
            if (!stagedReady && !game.saves.StageSlot(staged,stagedNew,stagedConfirmed))
            { Notice = game.saves.Status;Page = stagedNew ? MenuPage.PrepareFailed : MenuPage.LoadGame;busy = false;if(!stagedNew)QueueBackground();yield break; }
            Exception failure = null;
            try { if (!stagedReady) { game.flight.Tick(0);game.cameraFollow.Follow(0);game.environment.Refresh(0,true);game.world.BeginPreparation();game.biomeNavigator.RefreshLabel(); } }
            catch (Exception error) { failure = error; }
            if (failure != null) { Debug.LogWarning(failure.Message);FailPreparation("COULD NOT PREPARE THIS FLIGHT");yield break; }
            PreparationStatus="BUILDING WORLD";
            float deadline = Time.unscaledTime + game.preparationTimeoutSeconds;
            while ((!game.world.ViewReady||!game.world.CanCaptureSave)&&game.world.PreparationError==null&&Time.unscaledTime<deadline)yield return null;
            if(game.world.PreparationError!=null){FailPreparation("COULD NOT PREPARE THIS FLIGHT");yield break;}
            if (!game.world.ViewReady||!game.world.CanCaptureSave) { game.world.CancelPreparation();FailPreparation("FLIGHT PREPARATION TIMED OUT");yield break; }
            stagedReady = true;
            PreparationStatus=stagedNew?"SAVING FLIGHT":"READY";yield return null;
            if (!game.saves.CommitStagedSlot(stagedNew)) { FailPreparation("SAVE FAILED");yield break; }
            ShowBackground(true);Page = MenuPage.Running;SettingsOpen = DisplayOpen = false;Notice = "";game.session.menuPaused = false;busy = false;
        }
        void FailPreparation(string text) { if(game.world.PreparingView)game.world.CancelPreparation();Notice = text;Page = MenuPage.PrepareFailed;busy = false;game.saves.EndJourneyWithoutSaving(); }
        public void Retry() { if (Page == MenuPage.PrepareFailed && !busy) StartCoroutine(Prepare()); }
        public void Back()
        {
            if (busy) return;
            if (OverwriteOpen) { OverwriteOpen = false;return; }
            if (DisplayOpen) { game.display.RevertMode();DisplayOpen = false;return; }
            bool rebuild=Page==MenuPage.PrepareFailed||Page==MenuPage.Running||(!BackgroundReady&&backgroundRoutine==null);
            Page = MenuPage.Title;Notice = "";SettingsOpen = false;game.saves.EndJourneyWithoutSaving();game.session.menuPaused = true;
            if(rebuild)QueueBackground();
        }
        public void ShowCredits() { if (Page == MenuPage.Title) Page = MenuPage.Credits; }
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
            Leaving = LeaveTarget.None;TitleQuitOpen = false;OverwriteOpen = false;Notice = "";
            if (ShowHud) { game.session.menuPaused = false;game.saves.SuspendAutomatic = false; }
        }
        public bool CompleteLeave(bool save)
        {
            if (Leaving == LeaveTarget.None) return false;
            if (save && !SaveGame()) return false;
            var target = Leaving;game.saves.EndJourneyWithoutSaving();Leaving = LeaveTarget.None;SettingsOpen = DisplayOpen = false;
            game.session.menuPaused = true;
            if (target == LeaveTarget.Title) { Page = MenuPage.Title;Notice="";QueueBackground(); }
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
            else if (Leaving != LeaveTarget.None || TitleQuitOpen || OverwriteOpen) CancelLeave();
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
