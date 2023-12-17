namespace Rio.CommandPipeline {
    /// <summary>
    /// The CommandPipelineBroker class is responsible for managing and providing access to ICommandPipeline objects.
    /// </summary>
    public class CommandPipelineBroker {
        /// <summary>
        /// Gets the ICommandPipeline object with the specified id.
        /// </summary>
        /// <param name="id">The id of the ICommandPipeline object to retrieve.</param>
        /// <returns>The ICommandPipeline object with the specified id.</returns>
        /// <exception cref="KeyNotFoundException">Thrown when the key is not found in the event broker.</exception>
        /// <exception cref="NullReferenceException">Thrown when a producer with the specified id is not found.</exception>
        public ICommandPipeline this[string id] {
            get {
                if (!_producers.ContainsKey(id))
                   throw new KeyNotFoundException($"The key {id} could not be found in the event broker");

                var producer = _producers[id];

                if (producer == null)
                    throw new NullReferenceException($"Could not find a producer with the id {id}");
                
                return producer;
            }
        }

        /// <summary>
        /// Registers a producer with the given ID.
        /// </summary>
        /// <param name="id">The ID of the producer to register.</param>
        /// <param name="producer">The producer to register.</param>
        /// <returns>
        /// True if the registration is successful; otherwise, false.
        /// </returns>
        public bool Register(string id, ICommandPipeline producer) 
            => !string.IsNullOrEmpty(id) && _producers.TryAdd(id, producer);

        /// <summary>
        /// Unregisters a producer with the given ID.
        /// </summary>
        /// <param name="id">The ID of the producer to unregister.</param>
        /// <returns>Returns true if the producer was successfully unregistered, otherwise false.</returns>
        public bool Unregister(string id) 
            => !string.IsNullOrEmpty(id) && _producers.Remove(id);

#region PLUMBING

        /// <summary>
        /// Creates a new instance of the CommandPipelineBroker class.
        /// </summary>
        /// <param name="ids">The ids of the command pipeline producers.</param>
        public CommandPipelineBroker (params string [] ids) {
            foreach (var id in ids)
                if (!_producers.ContainsKey(id) && !string.IsNullOrEmpty(id))
                    _producers.Add(id, new CommandPipeline());
        }

        readonly Dictionary<string, ICommandPipeline?> _producers = new();

        #endregion

    }
}