using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;

namespace ObservableCollectionTest.Models
{
    public class StudentModelList : ObservableCollection<StudentModel>
    {
        /// <summary>
        /// 생성자
        /// </summary>
        public StudentModelList()
        {
            Add(new StudentModel() { Name = "범범조조", Age = 28, Grade = "A" });
            Add(new StudentModel() { Name = "안정환", Age = 20, Grade = "B" });
            Add(new StudentModel() { Name = "아이유", Age = 38, Grade = "D" });
            Add(new StudentModel() { Name = "정형돈", Age = 21, Grade = "F" });
            Add(new StudentModel() { Name = "유재석", Age = 74, Grade = "F" });
            Add(new StudentModel() { Name = "박명수", Age = 54, Grade = "D" });
            Add(new StudentModel() { Name = "하하", Age = 47, Grade = "A" });
            Add(new StudentModel() { Name = "광희", Age = 21, Grade = "B+" });
            Add(new StudentModel() { Name = "조세호", Age = 31, Grade = "C-" });
        }
    }

    public class StudentModel
    {
        public string Name { get; set; }
        public int Age { get; set; }
        public string Grade { get; set; }
    }
}
