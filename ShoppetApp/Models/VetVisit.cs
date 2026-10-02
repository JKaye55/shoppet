namespace ShoppetApp.Models;
public class VetVisit
{
 public int Id{get;set;}public int UserId{get;set;}public int PetId{get;set;}public string ClinicName{get;set;}="";public DateTime VisitAt{get;set;}=DateTime.Now.AddDays(1);public string Purpose{get;set;}="";public string Notes{get;set;}="";public bool Completed{get;set;}
 public string Display=>$"{VisitAt:MMM d, yyyy h:mm tt} · {ClinicName}";
}
