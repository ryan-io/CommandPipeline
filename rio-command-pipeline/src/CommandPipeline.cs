﻿using System.Diagnostics.CodeAnalysis;
using System.Text;
using Microsoft.Extensions.Logging;

namespace Rio.CommandPipeline {
	/// <summary>
	/// Represents a command pipeline that executes a series of asynchronous pipeline delegates.
	/// </summary>
	public class CommandPipeline : ICommandPipeline, ICommandPipelineSignals {
		/// <summary>
		/// Represents a delegate for an asynchronous command pipeline.
		/// </summary>
		/// <param name="o">The object to be passed to the pipeline.</param>
		/// <param name="pObj">The command pipeline arguments.</param>
		/// <param name="token">Cancellation token to cancel the pipeline execution.</param>
		/// <returns>A task representing the asynchronous pipeline execution.</returns>
		public delegate Task PipelineDelegate(object? o, PipelineObject? pObj, CancellationToken token);

		/// <summary>
		/// Gets or sets a value indicating whether logging is enabled on Command Pipeline.
		/// </summary>
		/// <value>
		///   <c>true</c> if logging is enabled; otherwise, <c>false</c>.
		/// </value>
		public bool IsLoggingEnabled { get; set; }
		
		/// <inheritdoc/>
		public ICommandPipelineSignals AuxiliarySignals => this;

		/// <inheritdoc/>
		public async Task SignalAsync(PipelineObject? pObj = default, CancellationToken? token = default) {
			if (RunHandlerAsync == null) {
				_sb.Append("RunHandlerAsync has no subscribers.");
				InternalLog();
				return;
			}

			pObj ??= PipelineObject.Empty;

			try {
				var listeners = new List<PipelineDelegate>();
				token ??= CancellationToken.None;

				pObj.CurrentState = CommandPipelineState.START;
				InvokeStartCallbacks(pObj);

				if (StartHandlerAsync != null)
					await InternalSignalAsync(StartHandlerAsync, listeners, pObj, token);

				pObj.CurrentState = CommandPipelineState.RUNNING;

				if (RunHandlerAsync != null)
					await InternalSignalAsync(RunHandlerAsync, listeners, pObj, token);

				pObj.CurrentState = CommandPipelineState.END;
				InvokeEndCallbacks(pObj);

				if (EndHandlerAsync != null)
					await InternalSignalAsync(EndHandlerAsync, listeners, pObj, token);
			}
			catch (Exception error) {
				if (_logger != null) {
					_sb.Append(error.Message);
					InternalLog(LogLevel.Error);
				}

				pObj.CurrentState = CommandPipelineState.ERROR;
				InvokeErrorCaughtCallbacks(ref pObj, ref error);
				InvokeErrorThrownCallbacks(ref pObj, ref error);
			}
			finally {
				if (_logger != null) {
					_sb.Append("Pipeline execution completed.");
					InternalLog();
				}

				_invocationList.Clear();
				pObj.CurrentState = CommandPipelineState.FINAL;
				InvokeFinallyCallbacks(pObj);
			}
		}

		/// <inheritdoc/>
		public ICommandPipeline RegisterWork(params PipelineDelegate[] subscribers) {
			InternalDelegateSubscribe(ref RunHandlerAsync, ref subscribers, Subscription.REGISTER);
			return this;
		}

		/// <inheritdoc/>
		public ICommandPipeline UnregisterWork(params PipelineDelegate[] subscribers) {
			InternalDelegateSubscribe(ref RunHandlerAsync, ref subscribers, Subscription.UNREGISTER);
			return this;
		}

		/// <inheritdoc/>
		public ICommandPipeline RegisterOnStart(params Action<PipelineObject>[] subscribers)
			=> InternalCallbackSubscribe(_startCallbacks, ref subscribers, Subscription.REGISTER);

		/// <inheritdoc/>
		public ICommandPipeline UnregisterOnStart(params Action<PipelineObject>[] subscribers)
			=> InternalCallbackSubscribe(_startCallbacks, ref subscribers, Subscription.UNREGISTER);

		/// <inheritdoc/>
		public ICommandPipeline RegisterOnEnd(params Action<PipelineObject>[] subscribers)
			=> InternalCallbackSubscribe(_endCallbacks, ref subscribers, Subscription.REGISTER);

		/// <inheritdoc/>
		public ICommandPipeline UnregisterOnEnd(params Action<PipelineObject>[] subscribers)
			=> InternalCallbackSubscribe(_endCallbacks, ref subscribers, Subscription.UNREGISTER);

		/// <inheritdoc/>
		public ICommandPipeline RegisterOnStartAsync(params PipelineDelegate[] subscribers)
			=> InternalDelegateSubscribe(ref StartHandlerAsync, ref subscribers, Subscription.REGISTER);

		/// <inheritdoc/>
		public ICommandPipeline UnregisterOnStartAsync(params PipelineDelegate[] subscribers)
			=> InternalDelegateSubscribe(ref StartHandlerAsync, ref subscribers, Subscription.UNREGISTER);

		/// <inheritdoc/>
		public ICommandPipeline RegisterOnEndAsync(params PipelineDelegate[] subscribers)
			=> InternalDelegateSubscribe(ref EndHandlerAsync, ref subscribers, Subscription.REGISTER);

		/// <inheritdoc/>
		public ICommandPipeline UnregisterOnEndAsync(params PipelineDelegate[] subscribers)
			=> InternalDelegateSubscribe(ref EndHandlerAsync, ref subscribers, Subscription.UNREGISTER);

		/// <inheritdoc/>
		public ICommandPipeline RegisterOnErrorCaught(params Action<PipelineObject, Exception>[] subscribers)
			=> InternalErrorSubscribe(_errorCaughtCallbacks, ref subscribers, Subscription.REGISTER);

		/// <inheritdoc/>
		public ICommandPipeline UnregisterOnErrorCaught(params Action<PipelineObject, Exception>[] subscribers)
			=> InternalErrorSubscribe(_errorCaughtCallbacks, ref subscribers, Subscription.UNREGISTER);

		/// <inheritdoc/>
		public ICommandPipeline RegisterOnErrorThrown(params Action<PipelineObject, Exception>[] subscribers)
			=> InternalErrorSubscribe(_errorThrownCallbacks, ref subscribers, Subscription.REGISTER);

		/// <inheritdoc/>
		public ICommandPipeline UnregisterOnErrorThrown(params Action<PipelineObject, Exception>[] subscribers)
			=> InternalErrorSubscribe(_errorThrownCallbacks, ref subscribers, Subscription.UNREGISTER);

		/// <inheritdoc/>
		public ICommandPipeline RegisterOnFinally(params Action<PipelineObject>[] subscribers)
			=> InternalCallbackSubscribe(_finallyCallbacks, ref subscribers, Subscription.REGISTER);

		/// <inheritdoc/>
		public ICommandPipeline UnregisterOnFinally(params Action<PipelineObject>[] subscribers)
			=> InternalCallbackSubscribe(_finallyCallbacks, ref subscribers, Subscription.UNREGISTER);

		/// <inheritdoc/>
		public void InvokeStartCallbacks()
			=> InternalInvokeCallback(_startCallbacks, ref PipelineObject.StaticRef);

		/// <inheritdoc/>
		public void InvokeEndCallbacks() => InternalInvokeCallback(_endCallbacks, ref PipelineObject.StaticRef);

		/// <inheritdoc/>
		public void InvokeFinallyCallbacks() {
			_sb.Append("Invoking finally callbacks.");
			InternalLog(LogLevel.Information);
			InternalInvokeCallback(_finallyCallbacks, ref PipelineObject.StaticRef);
		}

		/// <inheritdoc/>
		public void InvokeStartCallbacks(PipelineObject e) {
			_sb.Append("Invoking start callbacks.");
			InternalLog(LogLevel.Information);
			InternalInvokeCallback(_startCallbacks, ref e);
		}

		/// <inheritdoc/>
		public void InvokeEndCallbacks(PipelineObject e) {
			_sb.Append("Invoking end callbacks.");
			InternalLog(LogLevel.Information);
			InternalInvokeCallback(_endCallbacks, ref e);
		}

		/// <inheritdoc/>
		public void InvokeFinallyCallbacks(PipelineObject e) {
			_sb.Append("Invoking finally callbacks.");
			InternalLog(LogLevel.Information);
			InternalInvokeCallback(_finallyCallbacks, ref e);
		}

		/// <summary>
		/// Asynchronously invokes the given delegate and populates the listeners list with its invocation list.
		/// Then it asynchronously invokes each listener in the invocation list with the provided arguments and token.
		/// </summary>
		/// <param name="del">The delegate to be invoked.</param>
		/// <param name="listeners">The list to be populated with the invocation list of the delegate.</param>
		/// <param name="e">The CommandPipelineArgs arguments to be passed to each listener.</param>
		/// <param name="token">The CancellationToken to be passed to each listener.</param>
		/// <returns>A Task representing the asynchronous operation.</returns>
		async Task InternalSignalAsync(PipelineDelegate del, List<PipelineDelegate> listeners,
			PipelineObject e, CancellationToken? token) {
			_sb.Append($"Async signal started ({del.Method.Name}).");
			InternalLog(LogLevel.Information);
			listeners.Clear();
			_invocationList.Clear();
			listeners = del.GetInvocationList().DelegatesAs<PipelineDelegate>();

			token ??= CancellationToken.None;

			foreach (var listener in listeners)
				_invocationList.Add(listener.Invoke(this, e, token.Value));

			await Task.WhenAll(_invocationList);
			_sb.Append($"Async signal ended. ({del.Method.Name}).");
			InternalLog(LogLevel.Information);
		}

		/// <summary>
		/// Subscribes or unsubscribes the subscribers to the owner delegate based on the given subscription type.
		/// </summary>
		/// <param name="owner">The owner delegate to which the subscribers are subscribed or unsubscribed.</param>
		/// <param name="subscribers">The array of subscribers.</param>
		/// <param name="subscription">The type of subscription, either Register or Unregister.</param>
		/// <returns>The instance of the current object.</returns>
		/// <remarks>
		/// If the subscription type is Register, the method adds the subscribers to the owner delegate.
		/// If the subscription type is Unregister, the method removes the subscribers from the owner delegate.
		/// If the owner delegate is null, it does nothing and returns the current object.
		/// </remarks>
		ICommandPipeline InternalDelegateSubscribe(ref PipelineDelegate? owner,
			ref PipelineDelegate[] subscribers, Subscription subscription) {
			if (subscription == Subscription.REGISTER) {
				foreach (var subscriber in subscribers) {
					if (owner == null) 
						SubscribeToOwner(ref owner, subscriber);
					else {
						var invokeList = owner.GetInvocationList();

						if (invokeList.Contains(subscriber))
							continue;

						SubscribeToOwner(ref owner, subscriber);
					}
				}
			}

			else {
				if (owner == null)
					return this;

				var invokeList = owner.GetInvocationList();

				foreach (var subscriber in subscribers) {
					if (!invokeList.Contains(subscriber))
						continue;

					_sb.Append($"Removed {subscriber.Method.Name} to the invocation list of {owner!.Method.Name}.");
					owner -= subscriber.Invoke;
					InternalLog();
				}
			}

			return this;
		}

		/// <summary>
		/// Subscribes a subscriber method to the owner method.
		/// </summary>
		/// <param name="owner">The owner method to subscribe to.</param>
		/// <param name="subscriber">The subscriber method to be added to the owner's invocation list.</param>
		void SubscribeToOwner([AllowNull] ref PipelineDelegate owner, PipelineDelegate subscriber) {
			owner += subscriber.Invoke;
			_sb.Append($"Added {subscriber.Method.Name} to the invocation list of {owner.Method.Name}.");
			InternalLog();
		}

		/// <summary>
		/// Subscribes or unsubscribes the given callbacks from the command pipeline.
		/// </summary>
		/// <param name="callbacks">The set of callbacks to subscribe or unsubscribe.</param>
		/// <param name="subscribers">The array of subscribers.</param>
		/// <param name="subscription">The subscription type (Register or Unregister).</param>
		/// <returns>
		/// The current instance of the command pipeline after subscribing or unsubscribing the callbacks.
		/// </returns>
		ICommandPipeline InternalCallbackSubscribe(ISet<Action<PipelineObject>> callbacks,
			ref Action<PipelineObject>[] subscribers, Subscription subscription) {
			if (subscription == Subscription.REGISTER) {
				foreach (var cb in subscribers)
					callbacks.Add(cb);
			}

			else {
				foreach (var cb in subscribers)
					if (callbacks.Contains(cb))
						callbacks.Remove(cb);
			}

			return this;
		}

		/// <summary>
		/// Subscribes or unsubscribes a set of callback actions to handle internal error events.
		/// </summary>
		/// <param name="del">The set of callback actions to subscribe or unsubscribe.</param>
		/// <param name="subscribers">The array of subscribers to manage.</param>
		/// <param name="subscription">The type of subscription (Register or Unregister).</param>
		/// <returns>The ICommandPipeline instance.</returns>
		ICommandPipeline InternalErrorSubscribe(ISet<Action<PipelineObject, Exception>> del,
			ref Action<PipelineObject, Exception>[] subscribers, Subscription subscription) {
			if (subscription == Subscription.REGISTER) {
				foreach (var cb in subscribers)
					del.Add(cb);
			}
			else {
				foreach (var cb in subscribers)
					if (del.Contains(cb))
						del.Remove(cb);
			}

			return this;
		}

		/// <summary>
		/// Invokes the error caught callbacks in the command pipeline.
		/// </summary>
		/// <param name="pipelineObject">The command pipeline arguments.</param>
		/// <param name="error">The exception that was caught.</param>
		void InvokeErrorCaughtCallbacks(ref PipelineObject pipelineObject, ref Exception error) {
			foreach (var errorCaughtCallback in _errorCaughtCallbacks)
				errorCaughtCallback.Invoke(pipelineObject, error);
		}

		/// <summary>
		/// Invokes the error thrown callbacks with the provided CommandPipelineArgs and Exception objects.
		/// </summary>
		/// <param name="pObj">The CommandPipelineArgs object to pass to the error thrown callbacks.</param>
		/// <param name="error">The Exception object to pass to the error thrown callbacks.</param>
		void InvokeErrorThrownCallbacks(ref PipelineObject pObj, ref Exception error) {
			foreach (var errorThrownCallback in _errorThrownCallbacks)
				errorThrownCallback.Invoke(pObj, error);
		}

		/// <summary>
		/// Internal logging method.
		/// </summary>
		/// <param name="logLevel">Log level to output to</param>
		void InternalLog(LogLevel logLevel = LogLevel.Trace) {
			if (_logger == null)
				return;

			var time = DateTime.Now.TimeOfDay.ToString(@"hh\:mm\:ss");
			_logger.Log(logLevel, "{Time}" + " " + "{Output}", time, _sb.ToString());
			_sb.Clear();
		}

		/// <summary>
		/// Invokes the specified set of callbacks, passing the given command pipeline arguments.
		/// </summary>
		/// <param name="callback">The set of callbacks to be invoked.</param>
		/// <param name="e">The command pipeline arguments to be passed to the callbacks.</param>
		static void InternalInvokeCallback(HashSet<Action<PipelineObject>> callback, ref PipelineObject e) {
			foreach (var cb in callback)
				cb.Invoke(e);
		}

#region PLUMBING

		/// Initializes a new instance of the CommandPipeline class.
		/// @param logger (optional) The logger to be used for logging messages. If not provided, logging functionality will be disabled.
		/// /
		public CommandPipeline(ILogger? logger = null) {
			_logger = logger;
		}

		event PipelineDelegate? RunHandlerAsync;
		event PipelineDelegate? StartHandlerAsync;
		event PipelineDelegate? EndHandlerAsync;

		readonly ILogger?                                        _logger;
		readonly StringBuilder                                   _sb                   = new();
		readonly List<Task>                                      _invocationList       = new();
		readonly HashSet<Action<PipelineObject>>            _startCallbacks       = new();
		readonly HashSet<Action<PipelineObject>>            _endCallbacks         = new();
		readonly HashSet<Action<PipelineObject>>            _finallyCallbacks     = new();
		readonly HashSet<Action<PipelineObject, Exception>> _errorCaughtCallbacks = new();
		readonly HashSet<Action<PipelineObject, Exception>> _errorThrownCallbacks = new();

		/// <summary>
		/// Enum representing subscription options.
		/// </summary>
		enum Subscription : byte {
			REGISTER,
			UNREGISTER
		}

#endregion
	}

	/// <summary>
	/// Contains extension methods for internal use.
	/// </summary>
	internal static class InternalExtensions {
		/// <summary>
		/// Filters an array of delegates to return a list containing only delegates of a specific type.
		/// </summary>
		/// <typeparam name="T">The type of delegate to filter for.</typeparam>
		/// <param name="delegates">The array of delegates to filter.</param>
		/// <returns>A list of delegates of the specified type.</returns>
		internal static List<T> DelegatesAs<T>(this Delegate[] delegates) where T : Delegate {
			var output = new List<T>();

			foreach (var del in delegates) {
				if (del is not T asTypeDelegate)
					continue;

				output.Add(asTypeDelegate);
			}

			return output;
		}
	}

	public interface ICommandPipeline {
		/// <summary>
		/// Gets the auxiliary signals associated with the command pipeline.
		/// </summary>
		/// <value>
		/// The auxiliary signals.
		/// </value>
		ICommandPipelineSignals AuxiliarySignals { get; }

		/// <summary>
		/// Signals the asynchronous event by invoking the event handlers in the pipeline.
		/// </summary>
		/// <param name="pObj">The <see cref="PipelineObject"/> object containing the event data. </param>
		/// <param name="token">A <see cref="CancellationToken"/> to cancel the operation. If not specified, <see cref="CancellationToken.None"/> is used.</param>
		/// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
		Task SignalAsync(PipelineObject? pObj = default, CancellationToken? token = default);

		/// <summary>
		/// Registers the given subscribers to the command pipeline.
		/// </summary>
		/// <param name="subscribers">The subscribers to be registered as async pipeline delegates.</param>
		/// <returns>
		/// The current instance of the command pipeline.
		/// </returns>
		ICommandPipeline RegisterWork(params CommandPipeline.PipelineDelegate[] subscribers);

		/// <summary>
		/// Unregisters one or more subscribers from the command pipeline.
		/// </summary>
		/// <param name="subscribers">The list of subscribers to unregister.</param>
		/// <returns>The command pipeline instance.</returns>
		ICommandPipeline UnregisterWork(params CommandPipeline.PipelineDelegate[] subscribers);

		/// <summary>
		/// Registers asynchronous pipeline delegates to be executed on start.
		/// </summary>
		/// <param name="subscribers">The asynchronous pipeline delegates to be registered.</param>
		/// <returns>The command pipeline with the registered delegates.</returns>
		ICommandPipeline RegisterOnStartAsync(params CommandPipeline.PipelineDelegate[] subscribers);

		/// <summary>
		/// Unregisters the given subscribers from the <see cref="CommandPipeline.StartHandlerAsync"/> pipeline.
		/// </summary>
		/// <param name="subscribers">The list of subscribers to unregister.</param>
		/// <returns>The updated <see cref="ICommandPipeline"/> instance after unregistering the subscribers.</returns>
		ICommandPipeline UnregisterOnStartAsync(params CommandPipeline.PipelineDelegate[] subscribers);

		/// <summary>
		/// Registers one or more async pipeline delegates to be called when the pipeline ends.
		/// </summary>
		/// <param name="subscribers">One or more async pipeline delegates to be registered.
		/// </param>
		/// <returns>
		/// Returns an ICommandPipeline representing the pipeline instance, after registering the subscribers.
		/// </returns>
		ICommandPipeline RegisterOnEndAsync(params CommandPipeline.PipelineDelegate[] subscribers);

		/// <summary>
		/// Unregisters the specified subscribers from the <see cref="CommandPipeline.EndHandlerAsync"/> event.
		/// </summary>
		/// <param name="subscribers">The subscribers to be unregistered.</param>
		/// <returns>The instance of the <see cref="ICommandPipeline"/> after unregistering the subscribers.</returns>
		ICommandPipeline UnregisterOnEndAsync(params CommandPipeline.PipelineDelegate[] subscribers);

		/// <summary>
		/// Registers a list of subscribers to be called when the <see cref="CommandPipeline"/> starts.
		/// </summary>
		/// <param name="subscribers">The array of subscribers to register.</param>
		/// <returns>The <see cref="ICommandPipeline"/> instance.</returns>
		ICommandPipeline RegisterOnStart(params Action<PipelineObject>[] subscribers);

		/// <summary>
		/// Unregisters one or more subscribers from being invoked when the pipeline starts.
		/// </summary>
		/// <param name="subscribers">The array of subscribers to unregister.</param>
		/// <returns>The modified command pipeline instance after unregistering the subscribers.</returns>
		ICommandPipeline UnregisterOnStart(params Action<PipelineObject>[] subscribers);

		/// <summary>
		/// Registers one or more action delegates to be executed at the end of the command pipeline.
		/// </summary>
		/// <param name="subscribers">The action delegates to register.</param>
		/// <returns>The modified ICommandPipeline instance.</returns>
		ICommandPipeline RegisterOnEnd(params Action<PipelineObject>[] subscribers);

		/// <summary>
		/// Unregisters subscribers from the OnEnd event of ICommandPipeline.
		/// </summary>
		/// <param name="subscribers">The subscribers to unregister.</param>
		/// <returns>The modified ICommandPipeline instance.</returns>
		ICommandPipeline UnregisterOnEnd(params Action<PipelineObject>[] subscribers);

		/// <summary>
		/// Registers the given methods as subscribers to the 'finally' event of the command pipeline.
		/// </summary>
		/// <param name="subscribers">The methods to be registered as subscribers.</param>
		/// <returns>The updated command pipeline instance.</returns>
		ICommandPipeline RegisterOnFinally(params Action<PipelineObject>[] subscribers);

		/// <summary>
		/// Unregisters a set of subscribers from the OnFinally event. </summary>
		/// <param name="subscribers">A variable number of delegates that represent the subscribers to unsubscribe.</param>
		/// <returns>The instance of the ICommandPipeline on which this method was called.</returns>
		ICommandPipeline UnregisterOnFinally(params Action<PipelineObject>[] subscribers);

		/// <summary>
		/// Registers subscribers to be notified when an error is caught in the command pipeline.
		/// </summary>
		/// <param name="subscribers">An array of action methods that will be executed when an error is caught.</param>
		/// <returns>The modified command pipeline instance with the subscribers registered.</returns>
		/// <remarks>
		/// Subscribers will be executed in the order they are provided.
		/// </remarks>
		ICommandPipeline RegisterOnErrorCaught(params Action<PipelineObject, Exception>[] subscribers);

		/// <summary>
		/// Unregisters error caught subscribers from the ICommandPipeline.
		/// </summary>
		/// <param name="subscribers">An array of delegates to be unregistered.</param>
		/// <returns>The modified ICommandPipeline instance.</returns>
		ICommandPipeline UnregisterOnErrorCaught(params Action<PipelineObject, Exception>[] subscribers);

		/// <summary>
		/// Registers one or more subscribers to be executed when an error is thrown during command pipeline execution.
		/// </summary>
		/// <param name="subscribers">The subscribers to be executed. Each subscriber is a method that takes two parameters: CommandPipelineArgs and Exception.</param>
		/// <returns>
		/// An ICommandPipeline object.
		/// </returns>
		ICommandPipeline RegisterOnErrorThrown(params Action<PipelineObject, Exception>[] subscribers);

		/// <summary>
		/// Unregisters the specified subscribers from the internal error thrown event of the command pipeline.
		/// </summary>
		/// <param name="subscribers">The subscribers to unregister from the error thrown event.</param>
		/// <returns>An <see cref="ICommandPipeline"/> instance.</returns>
		ICommandPipeline UnregisterOnErrorThrown(params Action<PipelineObject, Exception>[] subscribers);
	}

	public interface ICommandPipelineSignals {
		/// <summary>
		/// Invokes the start callbacks registered in the command pipeline.
		/// </summary>
		void InvokeStartCallbacks();

		/// <summary>
		/// Invokes the end callbacks registered in the internal callback list.
		/// </summary>
		void InvokeEndCallbacks();

		/// <summary>
		/// Invokes the finally callbacks by passing the provided callbacks and a reference to the static CommandPipelineArgs. </summary>
		/// <remarks>
		/// This method executes the provided callbacks in the order they were added. It passes a reference to the
		/// static CommandPipelineArgs, which allows the callbacks to access or modify its properties. </remarks>
		void InvokeFinallyCallbacks();

		/// <summary>
		/// Invokes the start callbacks with the specified command pipeline arguments.
		/// </summary>
		/// <param name="e">The command pipeline arguments to pass to the callbacks.</param>
		void InvokeStartCallbacks(PipelineObject e);

		/// <summary>
		/// Invokes the end callbacks on the given <paramref name="e"/> CommandPipelineArgs.
		/// </summary>
		/// <param name="e">The CommandPipelineArgs to invoke end callbacks on.</param>
		void InvokeEndCallbacks(PipelineObject e);

		/// <summary>
		/// Invokes the finally callbacks for the given command pipeline arguments.
		/// </summary>
		/// <param name="e">The command pipeline arguments.</param>
		void InvokeFinallyCallbacks(PipelineObject e);
	}
}