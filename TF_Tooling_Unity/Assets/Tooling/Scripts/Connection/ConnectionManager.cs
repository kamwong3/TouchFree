using Newtonsoft.Json.Linq;
using System;
using System.IO;
using UnityEngine;
#if SCREENCONTROL_CORE
using Ultraleap.TouchFree.ServiceShared;
#endif

namespace Ultraleap.TouchFree.Tooling.Connection
{
    // Class: ConnectionManager
    // This Class manages the connection to the Service. It provides static variables
    // for ease of use and is a Singleton to allow for easy referencing.
    [RequireComponent(typeof(MessageReceiver), typeof(InputActionManager)), DisallowMultipleComponent, DefaultExecutionOrder(1)]
    public class ConnectionManager : MonoBehaviour
    {
        // Group: Variables

        // Variable: OnConnected
        // An event which is emitted when <Connect> is called.
        //
        // Instead of adding listeners to this event, use <AddConnectionListener> to ensure that your
        // function is invoked if the connection has already been made by the time your class runs.
        public static event Action OnConnected;

        // Variable: currentServiceConnection
        // The private reference to the currently managed <ServiceConnection>.
        static ServiceConnection currentServiceConnection;

        // Variable: serviceConnection
        // The public get-only reference to the currently managed <ServiceConnection>.
        public static ServiceConnection serviceConnection
        {
            get
            {
                return currentServiceConnection;
            }
        }

        // Variable: messageReceiver
        // A reference to the receiver that handles destribution of data received via the <currentServiceConnection> if connected.
        public static MessageReceiver messageReceiver;

        // Variable: HandFound
        // An event allowing users to react to a hand being found when none has been present for a moment.
        public static event Action HandFound;

        // Variable: HandsLost
        // An event allowing users to react to the last hand being lost when one has been present.
        public static event Action HandsLost;

        // Variable: HandEntered
        // An event allowing users to react to the active hand entering the interaction zone.
        public static event Action HandEntered;

        // Variable: HandExited
        // An event allowing users to react to the active hand exiting the interaction zone.
        public static event Action HandExited;

        static bool shouldReconnect = false;

        // Group: Functions

        // Function: AddConnectionListener
        // Used to both add the _onConnectFunc action to the listeners of <OnConnected>
        // as well as auto-call the _onConnectFunc if a connection is already made.
        public static void AddConnectionListener(Action _onConnectFunc)
        {
            OnConnected += _onConnectFunc;

            if (currentServiceConnection != null)
            {
                _onConnectFunc();
            }
        }

        // Function: Connect
        // Creates a new <ServiceConnection> using <iPAddress> and <port>.
        // Also invokes <OnConnected> on all listeners.
        public void Connect()
        {
            shouldReconnect = true;

            //Read IP and port from config before we attempt to connect
            string ip = "127.0.0.1";
            string port = "9739";
            string configPath = ConfigFileUtils.ConfigFileDirectory + "ServiceConfig.json";
            if (File.Exists(configPath))
            {
                JObject obj = JObject.Parse(File.ReadAllText(configPath));
                if (obj.ContainsKey("ServiceIP")) ip = obj["ServiceIP"].ToString();
                if (obj.ContainsKey("ServicePort")) port = obj["ServicePort"].ToString();
            }
            Debug.Log("Attempting to connect to TouchFree at: http://" + ip + ":" + port);

            currentServiceConnection = new ServiceConnection(ip, port, RetryConnecting);
            if (currentServiceConnection.IsConnected())
            {
                OnConnected?.Invoke();
            }
            else
            {
                RetryConnecting();
            }
        }

        public void RetryConnecting()
        {
            RetryConnecting(1000);
        }

        public async System.Threading.Tasks.Task RetryConnecting(int delay)
        {
            if (shouldReconnect) {
                await System.Threading.Tasks.Task.Delay(delay);
                if (!currentServiceConnection.IsConnected())
                {
                    currentServiceConnection.Connect();
                }

                RetryConnecting(Math.Max(delay * 2, 30000));
            }
        }

        // Function: Disconnect
        // Disconnects <currentServiceConnection> if it is connected to a WebSocket and
        // sets it to null.
        public static void Disconnect()
        {
            shouldReconnect = false;

            if (currentServiceConnection != null)
            {
                currentServiceConnection.Disconnect();
                currentServiceConnection = null;
            }
        }

        // Function: HandleHandPresenceEvent
        // Called by the <MessageReciever> to pass HandPresence events via the <HandFound> and
        // <HandsLost> events on this class
        internal static void HandleHandPresenceEvent(HandPresenceState _state)
        {
            if (_state == HandPresenceState.HAND_FOUND)
            {
                HandFound?.Invoke();
            }
            else
            {
                HandsLost?.Invoke();
            }
        }

        internal static void HandleInteractionZoneEvent(InteractionZoneState _state)
        {
            switch (_state)
            {
                case InteractionZoneState.HAND_ENTERED:
                    HandEntered?.Invoke();
                    break;
                case InteractionZoneState.HAND_EXITED:
                    HandExited?.Invoke();
                    break;
            }
        }

        // Group: Unity monoBehaviour overrides

        // Function: Awake
        // Run by Unity on Initialization. Finds the required <MessageReceiver> component.
        // Also attempts to immediately <Connect> to a WebSocket.
        private void Awake()
        {
            messageReceiver = GetComponent<MessageReceiver>();
            Connect();
        }

        // Function: OnEnable
        // Unity's OnEnable function for handling when the behaviour is enabled. Connects
        // to SC Service if not already connected.
        private void OnEnable()
        {
            if (currentServiceConnection == null)
            {
                Connect();
            }
        }

        // Function: OnDisable
        // Unity's OnDisable function for handling when the behaviour is disabled. Disconnects
        // from SC Service to prevent caching any new incoming inputs.
        private void OnDisable()
        {
            Disconnect();
        }

        // Function: OnDestroy
        // Unity's Destroy function for handling the deconstruction of a MonoBehaviour.
        // Ensures <Disconnect> is called.
        private void OnDestroy()
        {
            Disconnect();
        }

        // Function: RequestServiceStatus
        // Used to request a <ServiceStatus> from the Service via the <webSocket>.
        // Provides an asynchronous <ServiceStatus> via the _callback parameter.
        public static void RequestServiceStatus(Action<ServiceStatus> _callback)
        {
            ConnectionManager.serviceConnection.RequestServiceStatus(_callback);
        }
    }
}