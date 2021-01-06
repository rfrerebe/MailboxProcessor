using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;

namespace MailBoxTestApp
{
    /// <summary>
    /// Can be used to store Lazily created values (you can ask GetAgent even before it was added to the dictionary)
    /// It is used here for testing only
    /// </summary>
    /// <typeparam name="T"><![CDATA[in most cases here it was Agent<Message>]]></typeparam>
    public class AgentDictionary<T>
    {
        private readonly string[] _agentNames;
        private ConcurrentDictionary<string, Lazy<TaskCompletionSource<T>>> _agents = new ConcurrentDictionary<string, Lazy<TaskCompletionSource<T>>>();

        public string[] AgentNames => _agentNames;

        public AgentDictionary()
            : this(new string[] { "testMailbox1", "testMailbox2", "testMailbox3", "testMailbox4" })
        {

        }
        public AgentDictionary(string[] agentNames)
        {
            this._agentNames = agentNames;

            foreach (var agentName in _agentNames)
            {
                var lazyAgentVal = _agents.GetOrAdd(agentName, (_name) => new Lazy<TaskCompletionSource<T>>(() => new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously), true));
            }
        }

        private TaskCompletionSource<T> _GetAgent(string name)
        {
            if (_agents.TryGetValue(name, out var lazyVal))
            {
                return lazyVal.Value;
            }

            throw new Exception($"Agent {name} is not found in the dictionary");
        }

        public void AddAgent(string name, T agent)
        {
            var tcs = this._GetAgent(name);
            tcs.TrySetResult(agent);
        }

        public bool RemoveAgent(string name)
        {
            if (_agents.TryRemove(name, out var lazyVal))
            {
                Console.WriteLine($"Agent: {name} removed from dictionary");
                lazyVal.Value.TrySetCanceled();
                return true;
            }
            return false;
        }

        public async Task<T> GetAgent(string name)
        {
            return await this._GetAgent(name).Task;
        }

        public void Clear()
        {
            _agents.Keys.ToList().ForEach((name) => this.RemoveAgent(name));
        }
    }
}
