using System;
using System.Threading.Tasks;
using Unity.Networking.Transport.Relay;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;

namespace OnlyUp.Net
{
    /// <summary>
    /// Unity Relay: o'z serverimizsiz internet orqali ulanish. Brauzer ham ulana olishi uchun
    /// hamma platformada "wss" (xavfsiz WebSocket) ishlatiladi.
    /// </summary>
    static class RelayConnector
    {
        const string Protocol = "wss";

        static async Task SignInAsync()
        {
            if (UnityServices.State != ServicesInitializationState.Initialized)
                await UnityServices.InitializeAsync();
            // Ro'yxatdan o'tish shart emas: har bir qurilma nomsiz hisob bilan kiradi.
            if (!AuthenticationService.Instance.IsSignedIn)
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
        }

        public static async Task<(RelayServerData data, string code)> CreateAsync(int guests)
        {
            await SignInAsync();
            Allocation allocation = await RelayService.Instance.CreateAllocationAsync(guests);
            string code = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);
            return (allocation.ToRelayServerData(Protocol), code);
        }

        public static async Task<RelayServerData> JoinAsync(string code)
        {
            await SignInAsync();
            JoinAllocation allocation = await RelayService.Instance.JoinAllocationAsync(code);
            return allocation.ToRelayServerData(Protocol);
        }

        /// <summary>Xizmat xatosini o'yinchiga tushunarli matnga aylantiradi.</summary>
        public static string Describe(Exception error)
        {
            if (error is RelayServiceException relay)
                switch (relay.Reason)
                {
                    case RelayExceptionReason.JoinCodeNotFound:
                    case RelayExceptionReason.EntityNotFound:
                    case RelayExceptionReason.InvalidRequest:
                        return "Bunday kodli xona topilmadi";
                    case RelayExceptionReason.NetworkError:
                        return "Internet aloqasini tekshiring";
                    case RelayExceptionReason.RateLimited:
                        return "Juda ko'p urinish. Biroz kuting";
                }
            return "Online xizmatga ulanib bo'lmadi";
        }
    }
}
