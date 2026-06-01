using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Steamworks;

namespace Launcher
{
    // Информация о комнате (Steam-лобби) для списка на экране выбора.
    public class RoomInfo
    {
        public CSteamID LobbyId;
        public string Name = "";
        public int Members;
        public int MaxMembers = 2;
    }

    // Тонкая обёртка над Steamworks.NET: инициализация, лобби-«комнаты» и P2P-обмен.
    // Вся Steam-часть изолирована здесь, чтобы режим «Один компьютер» работал без Steam.
    public static class SteamManager
    {
        // Тег фильтрации лобби: в список попадают только наши комнаты.
        public const string GameTag = "MyGame_v1";
        public const int Channel = 0;

        // Ключи метаданных лобби.
        private const string KeyGame = "game";
        private const string KeyName = "name";

        public static bool Available { get; private set; }
        public static string LastError { get; private set; } = "";
        public static bool InRoom { get; private set; }
        public static CSteamID CurrentLobby { get; private set; }
        public static bool IsHost { get; private set; }
        public static CSteamID PeerId { get; private set; } = CSteamID.Nil;

        // События поднимаются на UI-потоке (коллбэки качаются таймером WinForms).
        public static event Action<List<RoomInfo>>? RoomListUpdated;
        public static event Action? RoomCreated;        // своя комната создана и мы в ней
        public static event Action<bool>? RoomEntered;  // вошли в комнату (arg = мы хост)
        public static event Action? RoomFailed;         // не удалось создать/войти
        public static event Action? PeerJoined;         // появился второй игрок
        public static event Action? PeerLeft;           // второй игрок вышел
        public static event Action<byte[]>? MessageReceived;
        public static event Action? LobbyDataChanged;   // обновились метаданные лобби

        private static Callback<LobbyDataUpdate_t>? _cbLobbyData;
        private static Callback<LobbyChatUpdate_t>? _cbChatUpdate;
        private static Callback<SteamNetworkingMessagesSessionRequest_t>? _cbSession;
        private static CallResult<LobbyCreated_t>? _crLobbyCreated;
        private static CallResult<LobbyMatchList_t>? _crLobbyList;
        private static CallResult<LobbyEnter_t>? _crLobbyEnter;

        private static System.Windows.Forms.Timer? _timer;
        private static string _pendingRoomName = "";

        // Инициализация Steam. Возвращает false, если Steam не запущен или нет нативных DLL —
        // в этом случае доступен только режим «Один компьютер».
        public static bool TryInit()
        {
            if (Available) return true;

            // Проверка соответствия нативной DLL обёртке Steamworks.NET (битность/версия).
            try
            {
                if (!Packsize.Test())
                {
                    LastError = "Packsize.Test() провален: несовпадение упаковки структур (неверная версия Steamworks.NET/SDK).";
                    return false;
                }
                if (!DllCheck.Test())
                {
                    LastError = "DllCheck.Test() провален: рядом с .exe лежит steam_api64.dll неверной версии.";
                    return false;
                }
            }
            catch (DllNotFoundException)
            {
                LastError = "steam_api64.dll не найден рядом с .exe (возьмите из Steamworks SDK: sdk/redistributable_bin/win64/steam_api64.dll).";
                return false;
            }
            catch (Exception ex)
            {
                LastError = "Ошибка проверки DLL: " + ex.Message;
                return false;
            }

            try
            {
                if (!SteamAPI.Init())
                {
                    LastError = "SteamAPI.Init() вернул false. Проверьте: Steam запущен и вы залогинены; steam_appid.txt (480) лежит рядом с .exe.";
                    return false;
                }
            }
            catch (DllNotFoundException)
            {
                LastError = "steam_api64.dll не найден рядом с .exe.";
                return false;
            }
            catch (Exception ex)
            {
                LastError = "Исключение SteamAPI.Init(): " + ex.Message;
                return false;
            }

            LastError = "";
            Available = true;

            _cbLobbyData  = Callback<LobbyDataUpdate_t>.Create(OnLobbyDataUpdate);
            _cbChatUpdate = Callback<LobbyChatUpdate_t>.Create(OnChatUpdate);
            _cbSession    = Callback<SteamNetworkingMessagesSessionRequest_t>.Create(OnSessionRequest);

            _timer = new System.Windows.Forms.Timer { Interval = 50 };
            _timer.Tick += (s, e) => Pump();
            _timer.Start();
            return true;
        }

        public static void Shutdown()
        {
            if (!Available) return;
            try { LeaveRoom(); } catch { }
            try { _timer?.Stop(); } catch { }
            try { SteamAPI.Shutdown(); } catch { }
            Available = false;
        }

        // Качаем коллбэки Steam и принимаем сетевые сообщения.
        private static void Pump()
        {
            if (!Available) return;
            try { SteamAPI.RunCallbacks(); } catch { }
            ReceiveMessages();
        }

        // ── Комнаты (лобби) ───────────────────────────────────────────────────────

        public static void CreateRoom(string name)
        {
            if (!Available) { RoomFailed?.Invoke(); return; }
            _pendingRoomName = string.IsNullOrWhiteSpace(name) ? "Комната" : name;
            SteamAPICall_t call = SteamMatchmaking.CreateLobby(ELobbyType.k_ELobbyTypePublic, 2);
            _crLobbyCreated ??= CallResult<LobbyCreated_t>.Create(OnLobbyCreated);
            _crLobbyCreated.Set(call);
        }

        public static void RefreshRoomList()
        {
            if (!Available) { RoomListUpdated?.Invoke(new List<RoomInfo>()); return; }
            // Серверный фильтр по нашему тегу — в выдачу попадают только наши комнаты.
            SteamMatchmaking.AddRequestLobbyListStringFilter(KeyGame, GameTag, ELobbyComparison.k_ELobbyComparisonEqual);
            SteamAPICall_t call = SteamMatchmaking.RequestLobbyList();
            _crLobbyList ??= CallResult<LobbyMatchList_t>.Create(OnLobbyList);
            _crLobbyList.Set(call);
        }

        public static void JoinRoom(CSteamID lobby)
        {
            if (!Available) { RoomFailed?.Invoke(); return; }
            SteamAPICall_t call = SteamMatchmaking.JoinLobby(lobby);
            _crLobbyEnter ??= CallResult<LobbyEnter_t>.Create(OnLobbyEnter);
            _crLobbyEnter.Set(call);
        }

        public static void LeaveRoom()
        {
            if (!Available || !InRoom) return;
            SteamMatchmaking.LeaveLobby(CurrentLobby);
            InRoom = false;
            IsHost = false;
            CurrentLobby = CSteamID.Nil;
            PeerId = CSteamID.Nil;
        }

        // Метаданные комнаты (только хост) — режим/фракции синхронизируются через лобби.
        public static void SetLobbyData(string key, string value)
        {
            if (Available && InRoom && IsHost) SteamMatchmaking.SetLobbyData(CurrentLobby, key, value);
        }

        public static string GetLobbyData(string key)
            => Available && InRoom ? SteamMatchmaking.GetLobbyData(CurrentLobby, key) : "";

        private static void OnLobbyCreated(LobbyCreated_t cb, bool ioFailure)
        {
            if (ioFailure || cb.m_eResult != EResult.k_EResultOK)
            {
                RoomFailed?.Invoke();
                return;
            }
            CurrentLobby = new CSteamID(cb.m_ulSteamIDLobby);
            InRoom = true;
            IsHost = true;
            PeerId = CSteamID.Nil;
            SteamMatchmaking.SetLobbyData(CurrentLobby, KeyGame, GameTag);
            SteamMatchmaking.SetLobbyData(CurrentLobby, KeyName, _pendingRoomName);
            RoomCreated?.Invoke();
            RoomEntered?.Invoke(true);
        }

        private static void OnLobbyList(LobbyMatchList_t cb, bool ioFailure)
        {
            var rooms = new List<RoomInfo>();
            if (!ioFailure)
            {
                for (int i = 0; i < cb.m_nLobbiesMatching; i++)
                {
                    CSteamID id = SteamMatchmaking.GetLobbyByIndex(i);
                    string name = SteamMatchmaking.GetLobbyData(id, KeyName);
                    rooms.Add(new RoomInfo
                    {
                        LobbyId = id,
                        Name = string.IsNullOrWhiteSpace(name) ? "Комната" : name,
                        Members = SteamMatchmaking.GetNumLobbyMembers(id),
                    });
                }
            }
            RoomListUpdated?.Invoke(rooms);
        }

        private static void OnLobbyEnter(LobbyEnter_t cb, bool ioFailure)
        {
            if (ioFailure || cb.m_EChatRoomEnterResponse != (uint)EChatRoomEnterResponse.k_EChatRoomEnterResponseSuccess)
            {
                RoomFailed?.Invoke();
                return;
            }
            CurrentLobby = new CSteamID(cb.m_ulSteamIDLobby);
            InRoom = true;
            IsHost = SteamMatchmaking.GetLobbyOwner(CurrentLobby) == SteamUser.GetSteamID();
            UpdatePeer();
            RoomEntered?.Invoke(IsHost);
            if (PeerId != CSteamID.Nil) PeerJoined?.Invoke();
        }

        private static void OnLobbyDataUpdate(LobbyDataUpdate_t cb) => LobbyDataChanged?.Invoke();

        private static void OnChatUpdate(LobbyChatUpdate_t cb)
        {
            var change = (EChatMemberStateChange)cb.m_rgfChatMemberStateChange;
            bool wasPeer = PeerId != CSteamID.Nil;
            UpdatePeer();
            bool isPeer = PeerId != CSteamID.Nil;

            if (!wasPeer && isPeer) PeerJoined?.Invoke();
            else if (wasPeer && !isPeer) PeerLeft?.Invoke();
        }

        // Пересчитать второго игрока (в комнате всегда 2 участника).
        private static void UpdatePeer()
        {
            PeerId = CSteamID.Nil;
            if (!InRoom) return;
            CSteamID me = SteamUser.GetSteamID();
            int n = SteamMatchmaking.GetNumLobbyMembers(CurrentLobby);
            for (int i = 0; i < n; i++)
            {
                CSteamID m = SteamMatchmaking.GetLobbyMemberByIndex(CurrentLobby, i);
                if (m != me) { PeerId = m; break; }
            }
        }

        // ── P2P-сообщения (SteamNetworkingMessages) ─────────────────────────────────

        private static void OnSessionRequest(SteamNetworkingMessagesSessionRequest_t cb)
        {
            var id = cb.m_identityRemote;
            SteamNetworkingMessages.AcceptSessionWithUser(ref id);
        }

        public static bool SendToPeer(byte[] data)
        {
            if (!Available || PeerId == CSteamID.Nil || data == null || data.Length == 0) return false;

            var identity = new SteamNetworkingIdentity();
            identity.SetSteamID(PeerId);

            IntPtr ptr = Marshal.AllocHGlobal(data.Length);
            try
            {
                Marshal.Copy(data, 0, ptr, data.Length);
                EResult r = SteamNetworkingMessages.SendMessageToUser(
                    ref identity, ptr, (uint)data.Length,
                    Constants.k_nSteamNetworkingSend_Reliable, Channel);
                return r == EResult.k_EResultOK;
            }
            finally { Marshal.FreeHGlobal(ptr); }
        }

        private static void ReceiveMessages()
        {
            var msgs = new IntPtr[16];
            int n = SteamNetworkingMessages.ReceiveMessagesOnChannel(Channel, msgs, msgs.Length);
            for (int i = 0; i < n; i++)
            {
                if (msgs[i] == IntPtr.Zero) continue;
                var m = SteamNetworkingMessage_t.FromIntPtr(msgs[i]);
                byte[] buf = new byte[m.m_cbSize];
                if (m.m_cbSize > 0 && m.m_pData != IntPtr.Zero)
                    Marshal.Copy(m.m_pData, buf, 0, m.m_cbSize);
                SteamNetworkingMessage_t.Release(msgs[i]);
                if (buf.Length > 0) MessageReceived?.Invoke(buf);
            }
        }
    }
}
