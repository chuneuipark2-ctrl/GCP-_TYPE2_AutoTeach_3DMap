using ObservableCollectionTest.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace ObservableCollectionTest.ViewModels
{
    public class StudentViewModel
    {
        private readonly StudentModelList items;

        public StudentViewModel()
        {
            this.items = new StudentModelList();
        }

        public StudentModelList Items
        {
            get { return this.items; }
        }
    }
}
