using TreeDataStructures.Core;

namespace TreeDataStructures.Implementations.AVL;

public class AvlTree<TKey, TValue> : BinarySearchTreeBase<TKey, TValue, AvlNode<TKey, TValue>>
    where TKey : IComparable<TKey>
{
    protected override AvlNode<TKey, TValue> CreateNode(TKey key, TValue value) => new AvlNode<TKey, TValue>(key, value);

    protected override void OnNodeAdded(AvlNode<TKey, TValue> newNode)
    {
        BalanceFromNode(newNode);
    }

    protected override void OnNodeRemoved(AvlNode<TKey, TValue>? parent, AvlNode<TKey, TValue>? child)
    {
        BalanceFromNode(parent);
    }

    private int GetHeight(AvlNode<TKey, TValue>? node) => node?.Height ?? 0;

    private void UpdateHeight(AvlNode<TKey, TValue> node)
    {
        node.Height = 1 + Math.Max(GetHeight(node.Left), GetHeight(node.Right));
    }

    protected override void RotateLeft(AvlNode<TKey, TValue> x)
    {
        base.RotateLeft(x);
        UpdateHeight(x);
        UpdateHeight(x.Parent!);
    }

    protected override void RotateRight(AvlNode<TKey, TValue> y)
    {
        base.RotateRight(y);
        UpdateHeight(y);
        UpdateHeight(y.Parent!);
    }

    private int BalanceFactor(AvlNode<TKey, TValue> node) => GetHeight(node.Left) - GetHeight(node.Right);

    private void BalanceFromNode(AvlNode<TKey, TValue>? node)
    {
        while (node != null)
        {
            UpdateHeight(node);
            int bf = BalanceFactor(node);

            if (bf > 1)
            {
                if (BalanceFactor(node.Left!) < 0)
                    RotateBigLeft(node);
                else
                    RotateRight(node);
            }
            else if (bf < -1)
            {
                if (BalanceFactor(node.Right!) > 0)
                    RotateBigRight(node);
                else
                    RotateLeft(node);
            }

            node = node.Parent;
        }
    }
}