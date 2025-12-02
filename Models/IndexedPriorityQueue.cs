namespace AnimeBingeDownloader.Models;

/// <summary>
/// A generic Indexed Priority Queue (Min-Heap implementation).
/// It allows for efficient O(log N) insertion, deletion, and, crucially,
/// updating the priority of an existing element.
/// </summary>
/// <typeparam name="TElement">The type of the elements to be stored (must be comparable for dictionary keys).</typeparam>
/// <typeparam name="TPriority">The type of the priority value (must be comparable).</typeparam>
public class IndexedPriorityQueue<TElement, TPriority>
    where TElement : notnull // Elements are used as keys in the Dictionary
    where TPriority : struct, IComparable, IConvertible // Priorities must be comparable
{
    // --- Internal Node Structure ---
    private record HeapNode(TElement Element, TPriority Priority);

    // --- Internal Data Structures ---
    
    // The heap itself, stored as a dynamic array (list).
    private readonly List<HeapNode> _heap = [];

    // A dictionary to map an element to its current index in the _heap list.
    // This allows O(1) lookup to find an element for priority updates.
    private readonly Dictionary<TElement, int> _elementToIndexMap = new();

    // Comparison for priorities (assuming a Min-Heap, lower TPriority value means higher urgency).
    private readonly Comparer<TPriority> _priorityComparer = Comparer<TPriority>.Default;

    // Thread synchronization object for parallel access.
    private readonly Lock _lock = new Lock();

    /// <summary>
    /// Gets the number of elements in the queue.
    /// </summary>
    public int Count
    {
        get { lock (_lock) { return _heap.Count; } }
    }

    /// <summary>
    /// Checks if the element is already present in the queue. O(1) complexity.
    /// </summary>
    public bool Contains(TElement element)
    {
        lock (_lock)
        {
            return _elementToIndexMap.ContainsKey(element);
        }
    }

    // ----------------------------------------------------------------------
    // CORE PUBLIC OPERATIONS
    // ----------------------------------------------------------------------

    /// <summary>
    /// Adds a new element with a given priority to the queue. O(log N) complexity.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown if the element is already present.</exception>
    public void Enqueue(TElement element, TPriority priority)
    {
        lock (_lock)
        {
            if (_elementToIndexMap.ContainsKey(element))
            {
                throw new InvalidOperationException($"Element '{element}' is already in the queue. Use UpdatePriority instead.");
            }

            // 1. Add the new node to the end of the heap list.
            _heap.Add(new HeapNode(element, priority));
            var index = _heap.Count - 1;

            // 2. Update the element-to-index map.
            _elementToIndexMap[element] = index;

            // 3. Restore the heap property by sifting the new element up.
            SiftUp(index);
        }
    }

    /// <summary>
    /// Extracts the element with the highest priority (lowest value). O(log N) complexity.
    /// </summary>
    /// <returns>The element with the highest priority.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the queue is empty.</exception>
    public TElement Dequeue()
    {
        lock (_lock)
        {
            if (_heap.Count == 0)
            {
                throw new InvalidOperationException("The queue is empty.");
            }

            // The element to return is always at the root (index 0).
            var rootNode = _heap[0];
            var element = rootNode.Element;

            // 1. Swap the root with the last element.
            Swap(0, _heap.Count - 1);

            // 2. Remove the former root (now at the end).
            _heap.RemoveAt(_heap.Count - 1);

            // 3. Remove the element from the index map.
            _elementToIndexMap.Remove(element);

            // 4. Restore the heap property by sifting the new root down (if the heap is not empty).
            if (_heap.Count > 0)
            {
                SiftDown(0);
            }

            return element;
        }
    }

    /// <summary>
    /// Updates the priority of an existing element. O(log N) complexity.
    /// This is the key feature of the Indexed Priority Queue.
    /// </summary>
    /// <exception cref="KeyNotFoundException">Thrown if the element is not present.</exception>
    public void UpdatePriority(TElement element, TPriority newPriority)
    {
        lock (_lock)
        {
            if (!_elementToIndexMap.TryGetValue(element, out int index))
            {
                throw new KeyNotFoundException($"Element '{element}' not found in the queue.");
            }

            var existingNode = _heap[index];
            var oldPriority = existingNode.Priority;

            // 1. Check if the priority actually changed.
            if (_priorityComparer.Compare(oldPriority, newPriority) == 0)
            {
                return; // No change needed.
            }

            // 2. Update the node in the heap array.
            _heap[index] = new HeapNode(element, newPriority);

            // 3. Restore the heap property based on whether the priority was increased or decreased.
            if (_priorityComparer.Compare(newPriority, oldPriority) < 0)
            {
                // Priority got higher (value got lower) -> Sift Up
                SiftUp(index);
            }
            else
            {
                // Priority got lower (value got higher) -> Sift Down
                SiftDown(index);
            }
        }
    }

    // ----------------------------------------------------------------------
    // HEAP MAINTENANCE METHODS (Private)
    // ----------------------------------------------------------------------

    /// <summary>
    /// Moves the node at the given index up the tree until the heap property is restored.
    /// </summary>
    private void SiftUp(int index)
    {
        var currentIndex = index;
        while (currentIndex > 0)
        {
            var parentIndex = (currentIndex - 1) / 2;
            
            // Compare the current node's priority with its parent.
            if (_priorityComparer.Compare(_heap[currentIndex].Priority, _heap[parentIndex].Priority) < 0)
            {
                // Current node has higher priority (lower value) than parent, so swap them.
                Swap(currentIndex, parentIndex);
                currentIndex = parentIndex;
            }
            else
            {
                // Heap property is satisfied, stop sifting.
                break;
            }
        }
    }

    /// <summary>
    /// Moves the node at the given index down the tree until the heap property is restored.
    /// </summary>
    private void SiftDown(int index)
    {
        var currentIndex = index;
        var count = _heap.Count; // Use _heap.Count directly

        while (true)
        {
            var leftChildIndex = 2 * currentIndex + 1;
            var rightChildIndex = 2 * currentIndex + 2;
            var smallestIndex = currentIndex;

            // Find the child with the smaller priority (for a min-heap).
            if (leftChildIndex < count && _priorityComparer.Compare(_heap[leftChildIndex].Priority, _heap[smallestIndex].Priority) < 0)
            {
                smallestIndex = leftChildIndex;
            }

            if (rightChildIndex < count && _priorityComparer.Compare(_heap[rightChildIndex].Priority, _heap[smallestIndex].Priority) < 0)
            {
                smallestIndex = rightChildIndex;
            }

            if (smallestIndex != currentIndex)
            {
                // Swap with the smaller child and continue sifting down.
                Swap(currentIndex, smallestIndex);
                currentIndex = smallestIndex;
            }
            else
            {
                // Heap property is satisfied, stop sifting.
                break;
            }
        }
    }

    /// <summary>
    /// Swaps two nodes in the heap array and updates their corresponding indices in the map.
    /// </summary>
    private void Swap(int indexA, int indexB)
    {
        // 1. Swap the nodes in the heap array.
        (_heap[indexA], _heap[indexB]) = (_heap[indexB], _heap[indexA]);

        // 2. Update the index map for the swapped elements.
        _elementToIndexMap[_heap[indexA].Element] = indexA;
        _elementToIndexMap[_heap[indexB].Element] = indexB;
    }
}

