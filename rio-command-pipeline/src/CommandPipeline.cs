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
		/// <param name="e">The command pipeline arguments.</param>
		/// <param name="token">Cancellation token to cancel the pipeline execution.</param>
		/// <returns>A task representing the asynchronous pipeline execution.</returns>
		public delegate Task CommandPipelineDelegate(object? o, CommandPipelineArgs? e, CancellationToken token);

		/// <inheritdoc/>
		public ICommandPipelineSignals AuxiliarySignals => this;

		/// <inheritdoc/>
		public async Task SignalAsync(CommandPipelineArgs? e = default, CancellationToken? token = default) {
			if (RunHandlerAsync == null)
				return;

			e ??= CommandPipelineArgs.Empty;

			try {
				var listeners = new List<CommandPipelineDelegate>();
				token ??= CancellationToken.None;

				e.CurrentState = CommandPipelineState.START;
				InvokeStartCallbacks(e);

				if (StartHandlerAsync != null)
					await InternalSignalAsync(StartHandlerAsync, listeners, e, token);

				e.CurrentState = CommandPipelineState.RUNNING;

				if (RunHandlerAsync != null)
					await InternalSignalAsync(RunHandlerAsync, listeners, e, token);

				e.CurrentState = CommandPipelineState.END;
				InvokeEndCallbacks(e);

				if (EndHandlerAsync != null)
					await InternalSignalAsync(EndHandlerAsync, listeners, e, token);
			}
			catch (Exception error) {
				if (_logger != null)
					InternalLog(error.Message);

				e.CurrentState = CommandPipelineState.ERROR;
				InvokeErrorCaughtCallbacks(ref e, ref error);
				InvokeErrorThrownCallbacks(ref e, ref error);
			}
			finally {
				if (_logger != null)
					InternalLog("Pipeline execution completed.");

				_invocationList.Clear();
				e.CurrentState = CommandPipelineState.FINAL;
				InvokeFinallyCallbacks(e);
			}
		}

		/// <inheritdoc/>
		public ICommandPipeline Register(params CommandPipelineDelegate[] subscribers) {
			InternalDelegateSubscribe(ref RunHandlerAsync, ref subscribers, Subscription.REGISTER);
			return this;
		}

		/// <inheritdoc/>
		public ICommandPipeline Unregister(params CommandPipelineDelegate[] subscribers) {
			InternalDelegateSubscribe(ref RunHandlerAsync, ref subscribers, Subscription.UNREGISTER);
			return this;
		}

		/// <inheritdoc/>
		public ICommandPipeline RegisterOnStart(params Action<CommandPipelineArgs>[] subscribers)
			=> InternalCallbackSubscribe(_startCallbacks, ref subscribers, Subscription.REGISTER);

		/// <inheritdoc/>
		public ICommandPipeline UnregisterOnStart(params Action<CommandPipelineArgs>[] subscribers)
			=> InternalCallbackSubscribe(_startCallbacks, ref subscribers, Subscription.UNREGISTER);

		/// <inheritdoc/>
		public ICommandPipeline RegisterOnEnd(params Action<CommandPipelineArgs>[] subscribers)
			=> InternalCallbackSubscribe(_endCallbacks, ref subscribers, Subscription.REGISTER);

		/// <inheritdoc/>
		public ICommandPipeline UnregisterOnEnd(params Action<CommandPipelineArgs>[] subscribers)
			=> InternalCallbackSubscribe(_endCallbacks, ref subscribers, Subscription.UNREGISTER);

		/// <inheritdoc/>
		public ICommandPipeline RegisterOnStartAsync(params CommandPipelineDelegate[] subscribers)
			=> InternalDelegateSubscribe(ref StartHandlerAsync, ref subscribers, Subscription.REGISTER);

		/// <inheritdoc/>
		public ICommandPipeline UnregisterOnStartAsync(params CommandPipelineDelegate[] subscribers)
			=> InternalDelegateSubscribe(ref StartHandlerAsync, ref subscribers, Subscription.UNREGISTER);

		/// <inheritdoc/>
		public ICommandPipeline RegisterOnEndAsync(params CommandPipelineDelegate[] subscribers)
			=> InternalDelegateSubscribe(ref EndHandlerAsync, ref subscribers, Subscription.REGISTER);

		/// <inheritdoc/>
		public ICommandPipeline UnregisterOnEndAsync(params CommandPipelineDelegate[] subscribers)
			=> InternalDelegateSubscribe(ref EndHandlerAsync, ref subscribers, Subscription.UNREGISTER);

		/// <inheritdoc/>
		public ICommandPipeline RegisterOnErrorCaught(params Action<CommandPipelineArgs, Exception>[] subscribers)
			=> InternalErrorSubscribe(_errorCaughtCallbacks, ref subscribers, Subscription.REGISTER);

		/// <inheritdoc/>
		public ICommandPipeline UnregisterOnErrorCaught(params Action<CommandPipelineArgs, Exception>[] subscribers)
			=> InternalErrorSubscribe(_errorCaughtCallbacks, ref subscribers, Subscription.UNREGISTER);

		/// <inheritdoc/>
		public ICommandPipeline RegisterOnErrorThrown(params Action<CommandPipelineArgs, Exception>[] subscribers)
			=> InternalErrorSubscribe(_errorThrownCallbacks, ref subscribers, Subscription.REGISTER);

		/// <inheritdoc/>
		public ICommandPipeline UnregisterOnErrorThrown(params Action<CommandPipelineArgs, Exception>[] subscribers)
			=> InternalErrorSubscribe(_errorThrownCallbacks, ref subscribers, Subscription.UNREGISTER);

		/// <inheritdoc/>
		public ICommandPipeline RegisterOnFinally(params Action<CommandPipelineArgs>[] subscribers)
			=> InternalCallbackSubscribe(_finallyCallbacks, ref subscribers, Subscription.REGISTER);

		/// <inheritdoc/>
		public ICommandPipeline UnregisterOnFinally(params Action<CommandPipelineArgs>[] subscribers)
			=> InternalCallbackSubscribe(_finallyCallbacks, ref subscribers, Subscription.UNREGISTER);

		/// <inheritdoc/>
		public void InvokeStartCallbacks()
			=> InternalInvokeCallback(_startCallbacks, ref CommandPipelineArgs.StaticRef);

		/// <inheritdoc/>
		public void InvokeEndCallbacks() => InternalInvokeCallback(_endCallbacks, ref CommandPipelineArgs.StaticRef);

		/// <inheritdoc/>
		public void InvokeFinallyCallbacks()
			=> InternalInvokeCallback(_finallyCallbacks, ref CommandPipelineArgs.StaticRef);

		/// <inheritdoc/>
		public void InvokeStartCallbacks(CommandPipelineArgs e) => InternalInvokeCallback(_startCallbacks, ref e);

		/// <inheritdoc/>
		public void InvokeEndCallbacks(CommandPipelineArgs e) => InternalInvokeCallback(_endCallbacks, ref e);

		/// <inheritdoc/>
		public void InvokeFinallyCallbacks(CommandPipelineArgs e) => InternalInvokeCallback(_finallyCallbacks, ref e);

		/// <summary>
		/// Asynchronously invokes the given delegate and populates the listeners list with its invocation list.
		/// Then it asynchronously invokes each listener in the invocation list with the provided arguments and token.
		/// </summary>
		/// <param name="del">The delegate to be invoked.</param>
		/// <param name="listeners">The list to be populated with the invocation list of the delegate.</param>
		/// <param name="e">The CommandPipelineArgs arguments to be passed to each listener.</param>
		/// <param name="token">The CancellationToken to be passed to each listener.</param>
		/// <returns>A Task representing the asynchronous operation.</returns>
		async Task InternalSignalAsync(CommandPipelineDelegate del, List<CommandPipelineDelegate> listeners,
			CommandPipelineArgs e, CancellationToken? token) {
			listeners.Clear();
			_invocationList.Clear();
			listeners = del.GetInvocationList().DelegatesAs<CommandPipelineDelegate>();

			token ??= CancellationToken.None;

			foreach (var listener in listeners)
				_invocationList.Add(listener.Invoke(this, e, token.Value));

			await Task.WhenAll(_invocationList);
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
		ICommandPipeline InternalDelegateSubscribe(ref CommandPipelineDelegate? owner,
			ref CommandPipelineDelegate[] subscribers, Subscription subscription) {
			if (subscription == Subscription.REGISTER) {
				foreach (var subscriber in subscribers) {
					if (owner == null)
						owner += subscriber.Invoke;
					else {
						var invokeList = owner.GetInvocationList();

						if (invokeList.Contains(subscriber))
							continue;

						owner += subscriber.Invoke;
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

					owner -= subscriber.Invoke;
				}
			}

			return this;
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
		ICommandPipeline InternalCallbackSubscribe(ISet<Action<CommandPipelineArgs>> callbacks,
			ref Action<CommandPipelineArgs>[] subscribers, Subscription subscription) {
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
		ICommandPipeline InternalErrorSubscribe(ISet<Action<CommandPipelineArgs, Exception>> del,
			ref Action<CommandPipelineArgs, Exception>[] subscribers, Subscription subscription) {
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
		/// <param name="args">The command pipeline arguments.</param>
		/// <param name="error">The exception that was caught.</param>
		void InvokeErrorCaughtCallbacks(ref CommandPipelineArgs args, ref Exception error) {
			foreach (var errorCaughtCallback in _errorCaughtCallbacks)
				errorCaughtCallback.Invoke(args, error);
		}

		/// <summary>
		/// Invokes the error thrown callbacks with the provided CommandPipelineArgs and Exception objects.
		/// </summary>
		/// <param name="args">The CommandPipelineArgs object to pass to the error thrown callbacks.</param>
		/// <param name="error">The Exception object to pass to the error thrown callbacks.</param>
		void InvokeErrorThrownCallbacks(ref CommandPipelineArgs args, ref Exception error) {
			foreach (var errorThrownCallback in _errorThrownCallbacks)
				errorThrownCallback.Invoke(args, error);
		}
		
		/// <summary>
		/// Internal logging method.
		/// </summary>
		/// <param name="output">The output message to be logged.</param>
		void InternalLog(string? output) {
			var time = DateTime.Now.TimeOfDay.ToString(@"hh\:mm\:ss");
			_logger?.LogError("{Time}" + " " + "{Output}", time, output);
		}

		/// <summary>
		/// Invokes the specified set of callbacks, passing the given command pipeline arguments.
		/// </summary>
		/// <param name="callback">The set of callbacks to be invoked.</param>
		/// <param name="e">The command pipeline arguments to be passed to the callbacks.</param>
		static void InternalInvokeCallback(HashSet<Action<CommandPipelineArgs>> callback, ref CommandPipelineArgs e) {
			foreach (var cb in callback)
				cb.Invoke(e);
		}

#region PLUMBING

		public CommandPipeline(ILogger? logger = null) {
			_logger = logger;
		}

		event CommandPipelineDelegate? RunHandlerAsync;
		event CommandPipelineDelegate? StartHandlerAsync;
		event CommandPipelineDelegate? EndHandlerAsync;

		readonly ILogger?                                        _logger;
		readonly List<Task>                                      _invocationList       = new();
		readonly HashSet<Action<CommandPipelineArgs>>            _startCallbacks       = new();
		readonly HashSet<Action<CommandPipelineArgs>>            _endCallbacks         = new();
		readonly HashSet<Action<CommandPipelineArgs>>            _finallyCallbacks     = new();
		readonly HashSet<Action<CommandPipelineArgs, Exception>> _errorCaughtCallbacks = new();
		readonly HashSet<Action<CommandPipelineArgs, Exception>> _errorThrownCallbacks = new();

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
		/// <param name="e">The <see cref="CommandPipelineArgs"/> object containing the event data. If not specified, <see cref="CommandPipelineArgs.Empty"/> is used.</param>
		/// <param name="token">A <see cref="CancellationToken"/> to cancel the operation. If not specified, <see cref="CancellationToken.None"/> is used.</param>
		/// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
		Task SignalAsync(CommandPipelineArgs? e = default, CancellationToken? token = default);

		/// <summary>
		/// Registers the given subscribers to the command pipeline.
		/// </summary>
		/// <param name="subscribers">The subscribers to be registered as async pipeline delegates.</param>
		/// <returns>
		/// The current instance of the command pipeline.
		/// </returns>
		ICommandPipeline Register(params CommandPipeline.CommandPipelineDelegate[] subscribers);

		/// <summary>
		/// Unregisters one or more subscribers from the command pipeline.
		/// </summary>
		/// <param name="subscribers">The list of subscribers to unregister.</param>
		/// <returns>The command pipeline instance.</returns>
		ICommandPipeline Unregister(params CommandPipeline.CommandPipelineDelegate[] subscribers);

		/// <summary>
		/// Registers asynchronous pipeline delegates to be executed on start.
		/// </summary>
		/// <param name="subscribers">The asynchronous pipeline delegates to be registered.</param>
		/// <returns>The command pipeline with the registered delegates.</returns>
		ICommandPipeline RegisterOnStartAsync(params CommandPipeline.CommandPipelineDelegate[] subscribers);

		/// <summary>
		/// Unregisters the given subscribers from the <see cref="CommandPipeline.StartHandlerAsync"/> pipeline.
		/// </summary>
		/// <param name="subscribers">The list of subscribers to unregister.</param>
		/// <returns>The updated <see cref="ICommandPipeline"/> instance after unregistering the subscribers.</returns>
		ICommandPipeline UnregisterOnStartAsync(params CommandPipeline.CommandPipelineDelegate[] subscribers);

		/// <summary>
		/// Registers one or more async pipeline delegates to be called when the pipeline ends.
		/// </summary>
		/// <param name="subscribers">One or more async pipeline delegates to be registered.
		/// </param>
		/// <returns>
		/// Returns an ICommandPipeline representing the pipeline instance, after registering the subscribers.
		/// </returns>
		ICommandPipeline RegisterOnEndAsync(params CommandPipeline.CommandPipelineDelegate[] subscribers);

		/// <summary>
		/// Unregisters the specified subscribers from the <see cref="CommandPipeline.EndHandlerAsync"/> event.
		/// </summary>
		/// <param name="subscribers">The subscribers to be unregistered.</param>
		/// <returns>The instance of the <see cref="ICommandPipeline"/> after unregistering the subscribers.</returns>
		ICommandPipeline UnregisterOnEndAsync(params CommandPipeline.CommandPipelineDelegate[] subscribers);

		/// <summary>
		/// Registers a list of subscribers to be called when the <see cref="CommandPipeline"/> starts.
		/// </summary>
		/// <param name="subscribers">The array of subscribers to register.</param>
		/// <returns>The <see cref="ICommandPipeline"/> instance.</returns>
		ICommandPipeline RegisterOnStart(params Action<CommandPipelineArgs>[] subscribers);

		/// <summary>
		/// Unregisters one or more subscribers from being invoked when the pipeline starts.
		/// </summary>
		/// <param name="subscribers">The array of subscribers to unregister.</param>
		/// <returns>The modified command pipeline instance after unregistering the subscribers.</returns>
		ICommandPipeline UnregisterOnStart(params Action<CommandPipelineArgs>[] subscribers);

		/// <summary>
		/// Registers one or more action delegates to be executed at the end of the command pipeline.
		/// </summary>
		/// <param name="subscribers">The action delegates to register.</param>
		/// <returns>The modified ICommandPipeline instance.</returns>
		ICommandPipeline RegisterOnEnd(params Action<CommandPipelineArgs>[] subscribers);

		/// <summary>
		/// Unregisters subscribers from the OnEnd event of ICommandPipeline.
		/// </summary>
		/// <param name="subscribers">The subscribers to unregister.</param>
		/// <returns>The modified ICommandPipeline instance.</returns>
		ICommandPipeline UnregisterOnEnd(params Action<CommandPipelineArgs>[] subscribers);

		/// <summary>
		/// Registers the given methods as subscribers to the 'finally' event of the command pipeline.
		/// </summary>
		/// <param name="subscribers">The methods to be registered as subscribers.</param>
		/// <returns>The updated command pipeline instance.</returns>
		ICommandPipeline RegisterOnFinally(params Action<CommandPipelineArgs>[] subscribers);

		/// <summary>
		/// Unregisters a set of subscribers from the OnFinally event. </summary>
		/// <param name="subscribers">A variable number of delegates that represent the subscribers to unsubscribe.</param>
		/// <returns>The instance of the ICommandPipeline on which this method was called.</returns>
		ICommandPipeline UnregisterOnFinally(params Action<CommandPipelineArgs>[] subscribers);

		/// <summary>
		/// Registers subscribers to be notified when an error is caught in the command pipeline.
		/// </summary>
		/// <param name="subscribers">An array of action methods that will be executed when an error is caught.</param>
		/// <returns>The modified command pipeline instance with the subscribers registered.</returns>
		/// <remarks>
		/// Subscribers will be executed in the order they are provided.
		/// </remarks>
		ICommandPipeline RegisterOnErrorCaught(params Action<CommandPipelineArgs, Exception>[] subscribers);

		/// <summary>
		/// Unregisters error caught subscribers from the ICommandPipeline.
		/// </summary>
		/// <param name="subscribers">An array of delegates to be unregistered.</param>
		/// <returns>The modified ICommandPipeline instance.</returns>
		ICommandPipeline UnregisterOnErrorCaught(params Action<CommandPipelineArgs, Exception>[] subscribers);

		/// <summary>
		/// Registers one or more subscribers to be executed when an error is thrown during command pipeline execution.
		/// </summary>
		/// <param name="subscribers">The subscribers to be executed. Each subscriber is a method that takes two parameters: CommandPipelineArgs and Exception.</param>
		/// <returns>
		/// An ICommandPipeline object.
		/// </returns>
		ICommandPipeline RegisterOnErrorThrown(params Action<CommandPipelineArgs, Exception>[] subscribers);

		/// <summary>
		/// Unregisters the specified subscribers from the internal error thrown event of the command pipeline.
		/// </summary>
		/// <param name="subscribers">The subscribers to unregister from the error thrown event.</param>
		/// <returns>An <see cref="ICommandPipeline"/> instance.</returns>
		ICommandPipeline UnregisterOnErrorThrown(params Action<CommandPipelineArgs, Exception>[] subscribers);
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
		void InvokeStartCallbacks(CommandPipelineArgs e);

		/// <summary>
		/// Invokes the end callbacks on the given <paramref name="e"/> CommandPipelineArgs.
		/// </summary>
		/// <param name="e">The CommandPipelineArgs to invoke end callbacks on.</param>
		void InvokeEndCallbacks(CommandPipelineArgs e);

		/// <summary>
		/// Invokes the finally callbacks for the given command pipeline arguments.
		/// </summary>
		/// <param name="e">The command pipeline arguments.</param>
		void InvokeFinallyCallbacks(CommandPipelineArgs e);
	}
}