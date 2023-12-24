namespace SharpTriStrip
{
	/// <summary>
	/// Provides simple cache with an internal array of certain length.
	/// </summary>
	public class VertexCache
	{
		private readonly int[] m_entries;

		/// <summary>
		/// Gets or sets value at a certain index provided. Note that this does not do out of bounds check.
		/// </summary>
		/// <param name="index">Index to get or set value at.</param>
		/// <returns><see cref="System.Int32"/> at the index provided.</returns>
		public int this[int index]
		{
			get => this.m_entries[index];
			set => this.m_entries[index] = value;
		}

		/// <summary>
		/// Creates a new instance of <see cref="VertexCache"/> with default cache of size 16.
		/// </summary>
		public VertexCache() : this(16)
		{
		}

		/// <summary>
		/// Creates a new instance of <see cref="VertexCache"/> with cache of size given.
		/// </summary>
		/// <param name="size">Size of the internal cache. Note that this does not do out of range check.</param>
		public VertexCache(int size)
		{
			var entries = new int[size];

			for (int i = 0; i < entries.Length; ++i)
			{
				entries[i] = -1;
			}

			this.m_entries = entries;
		}

		/// <summary>
		/// Checks whether value provided is in cache.
		/// </summary>
		/// <param name="entry">Value to check.</param>
		/// <returns><see langword="true"/> if value is in cache; otherwise, <see langword="false"/>.</returns>
		public bool InCache(int entry)
		{
			var entries = this.m_entries;

			for (int i = 0; i < entries.Length; ++i)
			{
				if (entries[i] == entry)
				{
					return true;
				}
			}

			return false;
		}

		/// <summary>
		/// Adds value and returns its index in the cache.
		/// </summary>
		/// <param name="entry">Value to add.</param>
		/// <returns>Index of the added value in the cache.</returns>
		public int AddEntry(int entry)
		{
			var entries = this.m_entries;

			int removed = entries[entries.Length - 1];

			for (int i = entries.Length - 2; i >= 0; --i)
			{
				entries[i + 1] = entries[i];
			}

			entries[0] = entry;

			return removed;
		}

		/// <summary>
		/// Clears cache and all its values.
		/// </summary>
		public void Clear()
		{
			var entries = this.m_entries;

			for (int i = 0; i < entries.Length; ++i)
			{
				entries[i] = -1;
			}
		}

		/// <summary>
		/// Copies values of this cache into other cache.
		/// </summary>
		/// <param name="other"><see cref="VertexCache"/> to copy values to.</param>
		public void Copy(VertexCache other)
		{
			var thisEntries = this.m_entries;
			var copyEntries = other.m_entries;

			for (int i = 0; i < thisEntries.Length; ++i)
			{
				copyEntries[i] = thisEntries[i];
			}
		}
	}
}
