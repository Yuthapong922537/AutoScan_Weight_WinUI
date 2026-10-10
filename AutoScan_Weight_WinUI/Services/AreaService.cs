using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AutoScan_Weight_WinUI.Models;
using AutoScan_Weight_WinUI.Repositories;

namespace AutoScan_Weight_WinUI.Services
{
    public class AreaService
    {
        private readonly AreaRepository _repository = new();
    

    public List<AreaModel> GetAreas()
        {
            return _repository.GetAreas();
        }

        public List<AreaModel> GetAreaByOrder(string orderNo)
        {
            return _repository.GetAreaByOrder(orderNo);
        }
        public List<PalletDetailModel> GetDocByOrder(string orderNo , string docNo)
        {
            return _repository.GetDocNoByOrder(orderNo , docNo);
        }

        public List<PalletDetailModel> GetFinalByOrder(string orderNo, string docNo, string final)
        {
            return _repository.GetFinalDestByOrder(orderNo, docNo, final);
        }

        public List<PalletDetailModel> GetCartonByOrder(string orderNo, string docNo, string final, string canton)
        {
            return _repository.GetScancartonByOrder(orderNo, docNo, final,canton);
        }
        public List<PalletDetailModel> GetAreaByPackinglist(string packing)
        {
            return _repository.GetPackingList(packing);
        }

        public bool SwapArea(AreaModel source, AreaModel target)
        {
            // =========================
            // เก็บค่าก่อน Swap
            // =========================

            string sourceAreaNo = source.AreaNo;
            string sourcePalletNo = source.PalletsNo;

            string targetAreaNo = target.AreaNo;
            string targetPalletNo = target.PalletsNo;

            System.Diagnostics.Debug.WriteLine($"Swap {source.AreaNo} <-> {target.AreaNo}");

            //WarehouseName
            (source.WarehouseName, target.WarehouseName) = (target.WarehouseName, source.WarehouseName);
            // Pallet
            (source.PalletsNo, target.PalletsNo) = (target.PalletsNo, source.PalletsNo);

            // Customer
            (source.Customer, target.Customer) = (target.Customer, source.Customer);

            // StateArea
            (source.Status, target.Status) = (target.Status, source.Status);
            // GroupKey
            (source.GroupKey, target.GroupKey) = (target.GroupKey, source.GroupKey);
            // GroupType
            (source.GroupType, target.GroupType) = (target.GroupType, source.GroupType);
            // CartonCount
            (source.CartonCount, target.CartonCount) = (target.CartonCount, source.CartonCount);
            // MaxCapacity
            (source.MaxCapital, target.MaxCapital) = (target.MaxCapital, source.MaxCapital);
            // PalletStatus
            (source.PalletStatus, target.PalletStatus) = (target.PalletStatus, source.PalletStatus);

            //return _repository.SwapArea(source, target);

            return _repository.SwapArea(source,target,sourceAreaNo,sourcePalletNo,targetAreaNo,targetPalletNo);


        }
    }
}
