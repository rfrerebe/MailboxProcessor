﻿using MailboxProcessor;
using MailBoxTestApp.Handlers;
using MailBoxTestApp.Messages;
using System;
using System.Threading;

namespace MailBoxTestApp
{
    public static class AgentFactory
    {
        public static IAgent<Message> CreateFileAgent(string filePath, AgentOptions<Message> agentOptions= null)
        {
            FileAgentHandler fileAgentHandler = new FileAgentHandler(filePath);

            agentOptions = agentOptions ?? AgentOptions<Message>.Default;
            agentOptions.ScanHandler = fileAgentHandler;


            var agent = new Agent<Message>(fileAgentHandler, agentOptions);

            agent.AgentStarting += (s, a) => {
                 int threadId = Thread.CurrentThread.ManagedThreadId;
                 Console.WriteLine($"Starting MailboxProcessor Thread: {threadId} File: {filePath}");
            };

            agent.AgentStopped += (s, a) => {
                int threadId = Thread.CurrentThread.ManagedThreadId;
                Console.WriteLine($"Stopping MailboxProcessor Thread: {threadId} File: {filePath}");
            };

            agent.Start();

            return agent;
        }

        public static IAgent<Message> CreateCoordinatorAgent(AgentOptions<Message> agentOptions = null)
        {
            var handler = new CoordinatorAgentHandler();
            var agent = new Agent<Message>(handler, agentOptions);

            agent.AgentStarting += (s, a) => {
                Console.WriteLine($"Starting Coordinator");
            };

            agent.AgentStopped += (s, a) => {
                Console.WriteLine($"Stopping Coordinator");
            };

            agent.Start();

            return agent;
        }
    }
}
