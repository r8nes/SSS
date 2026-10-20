using System.Collections.Generic;
using System.Linq;
using Sirenix.OdinInspector;
using UnityEngine;

namespace ShooterShop.Catalog
{
    [CreateAssetMenu(menuName = "Shooter Shop/Catalog/Product Catalog", fileName = "ProductCatalog")]
    public sealed class ProductCatalog : ScriptableObject
    {
        [ListDrawerSettings(ShowFoldout = false), Required, ValidateInput(nameof(HasNoDuplicates), "Catalog contains duplicate products")]
        [SerializeField] private List<ProductConfig> _products = new();

        public IReadOnlyList<ProductConfig> Products => _products;

        public int IndexOf(ProductConfig product) => _products.IndexOf(product);

        public bool TryGet(int index, out ProductConfig product)
        {
            var isValid = index >= 0 && index < _products.Count;
            product = isValid ? _products[index] : null;
            return isValid;
        }

        public IEnumerable<ProductConfig> GetByCategory(ProductCategory category) =>
            _products.Where(p => p.Category == category);

        private bool HasNoDuplicates(List<ProductConfig> products) =>
            products.Distinct().Count() == products.Count;
    }
}
