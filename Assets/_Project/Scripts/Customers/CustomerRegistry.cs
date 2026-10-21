using System.Collections.Generic;
using UnityEngine;
using VContainer.Unity;

namespace ShooterShop.Customers
{
    public sealed class CustomerRegistry : ITickable
    {
        private readonly List<Customer> _customers = new();

        public void Add(Customer customer) => _customers.Add(customer);

        public void Remove(Customer customer) => _customers.Remove(customer);

        public void Tick()
        {
            var deltaTime = Time.deltaTime;

            for (var i = _customers.Count - 1; i >= 0; i--)
            {
                if (_customers[i] == null)
                    _customers.RemoveAt(i);
                else
                    _customers[i].ClientTick(deltaTime);
            }
        }
    }
}
