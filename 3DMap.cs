using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace gcp_Wpf
{
    //260714 PCE 3d_Mapping code 추가
    class _3DMap
    {
        public static Dictionary<string, bool> Build(string assyId, ref SRM_IO io)
        {
            var d =  new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);

            if (string.Equals(assyId, "SCP", StringComparison.OrdinalIgnoreCase))
            {
                //좌측: 3D MAP 기준, 우측: MICOM 기준(싱글톤 클래스)

                d["Lift_Out_Limit_Sensor_E"] = io.LST;
                //d["Lift_Out_Limit_Sensor_H"] = io.LST;
                //d["Lift_Brake_MMS_FLT"] = io./* */;
                d["Panel_EMO_SW"] = io.EM;
                d["Auto_Key_SW"] = io.AUTO;
                d["Maint_Key_SW"] = io.MAN;
                //d["Panel_Reset_SW"] = io./* */;
                //d["Over_Temp_Sensor"] = io./* */;
                //d["GOV"] = io./* */;
                //d["Safety_Relay_Alarm"] = io./* */;
                //d["INV_MDR_Ready"] = io./* */;
                //d["INV_MDR_MC_FeedBack"] = io./* */;
                d["INV_Inhibit"] = io.IINH;
                //d["INV_Reset"] = io./* */;
                d["Tower_Lamp_RED"] = io.RED;
                d["Tower_Lamp_Yellow"] = io.YEL;
                d["Tower_Lamp_Green"] = io.GRN;
                //d["Tower_Lamp_White"] = io./* */;
                //d["Tower_Lamp_Blue"] = io./* */;
                d["Tower_Lamp_Buzzer"] = io.SUD;
                //d["M_Cage_EMS"] = io./* */;
                //d["M_Cage_Key_SW"] = io./* */;
                //d["M_Cage_Error"] = io./* */;
                //d["M_Cage_Door_SW"] = io./* */;
            }
            else if (string.Equals(assyId, "Lower_Frame_assy", StringComparison.OrdinalIgnoreCase))
            {
                d["Travel_Limit_Sensor_H"] = io.TST;
                d["Travel_Limit_Sensor_E"] = io.TST;
                //d["Travel1_Brake_MMS_FLT"] = io./* */;
                //d["Travel2_Brake_MMS_FLT"] = io./* */;
                d["Travel_DEC"] = io.TDF;
                d["Travel_Home_Position"] = io.THP;
                d["Modem_Fault"] = io.MFLT;
                d["Station_Ready_1"] = io.CVNO1;
                d["Station_Ready_2"] = io.CVNO2;
                d["Station_Ready_3"] = io.CVNO3;
                d["Station_Ready_4"] = io.CVNO4;
                d["Station_Ready_5"] = io.CVNO5;
                d["Station_Ready_6"] = io.CVNO6;
                d["Station_Ready_7"] = io.CVNO7;
                d["Station_Ready_8"] = io.CVNO8;
                d["Station_Stop_1"] = io.CVOK1;
                d["Station_Stop_2"] = io.CVOK2;
                d["Station_Stop_3"] = io.CVOK3;
                d["Station_Stop_4"] = io.CVOK4;
                d["Station_Stop_5"] = io.CVOK5;
                d["Station_Stop_6"] = io.CVOK6;
                d["Station_Stop_7"] = io.CVOK7;
                d["Station_Stop_8"] = io.CVOK8;
            }
            else if (string.Equals(assyId, "Carriage_Assy", StringComparison.OrdinalIgnoreCase))
            {
                d["Rope_Tension_Rear"] = io.RTR;
                d["Rope_Tension_Front"] = io.RTF;
                d["Lift_DEC"] = io.LDD;
                d["Lift_Home_Position"] = io.LHP;
                //d["Lift_In_Limit_Sensor_H"] = io./* */;
                //d["Lift_In_Limit_Sensor_E"] = io./* */;
                d["Fork_Center_Left"] = io.FCL1;
                d["Fork_Center_Right"] = io.FCR1;
                d["Fork_Out_Left"] = io.FOKL1;
                d["Fork_Out_Right"] = io.FOKR1;
                d["Fork_Half_Left"] = io.FHL1;
                d["Fork_Half_Right"] = io.FHR1;
                d["Fork_End_Left"] = io.FEL1;
                d["Fork_End_Right"] = io.FER1;
                //d["Fork_Staion_Left"] = io./* */;
                //d["Fork_Staion_Right"] = io./* */;
                //d["Fork1_Brake_MMS_FLT"] = io./* */;
                //d["Fork2_Brake_MMS_FLT"] = io./* */;
                //d["Fork1_Alarm"] = io./* */;
                //d["Fork1_Stop"] = io./* */;
                //d["Fork2_Alarm"] = io./* */;
                //d["Fork2_Stop"] = io./* */;
                //d["Fork1_STO_1"] = io./* */;
                //d["Fork1_STO_2"] = io./* */;
                //d["Fork1_JOG_Mode"] = io./* */;
                //d["Fork1_Rotate_Right"] = io./* */;
                //d["Fork1_Rotate_Left"] = io./* */;
                //d["Fork1_Alarm_Reset"] = io./* */;
                //d["Fork2_STO_1"] = io./* */;
                //d["Fork2_STO_2"] = io./* */;
                //d["Fork2_JOG_Mode"] = io./* */;
                //d["Fork2_Rotate_Right"] = io./* */;
                //d["Fork2_Rotate_Left"] = io./* */;
                //d["Fork2_Alarm_Reset"] = io./* */;
                d["Goods_OX"] = io.GOX1;
                //d["Goods_High_OX"] = io./* */;
                //d["Goods_Middle_OX"] = io./* */;
                //d["Goods_Small_OX"] = io./* */;
                d["Goods_Width_Left"] = io.GWL1;
                d["Goods_Width_Right"] = io.GWR1;
                d["Goods_Depth_Front_L"] = io.GDFL1;
                d["Goods_Depth_Front_R"] = io.GDFR1;
                d["Goods_Depth_Rear_L"] = io.GDRL1;
                d["Goods_Depth_Rear_R"] = io.GDRR1;
                d["Goods_Height_Left"] = io.GHL1;
                d["Goods_Height_Right"] = io.GHR1;
                //d["Goods_OX2"] = io./* */;
                //d["Goods_High_OX2"] = io./* */;
                //d["Goods_Middle_OX2"] = io./* */;
                //d["Goods_Small_OX2"] = io./* */;
                //d["Goods_Width_Left2"] = io./* */;
                //d["Goods_Width_Right2"] = io./* */;
                //d["Goods_Depth_Front_L2"] = io./* */;
                //d["Goods_Depth_Front_R2"] = io./* */;
                //d["Goods_Depth_Rear_L2"] = io./* */;
                //d["Goods_Depth_Rear_R2"] = io./* */;
                //d["Goods_Height_Left2"] = io./* */;
                //d["Goods_Height_Right2"] = io./* */;
                //d["Fork_Center_Left2"] = io./* */;
                //d["Fork_Center_Right2"] = io./* */;
                //d["Fork_Out_Left2"] = io./* */;
                //d["Fork_Out_Right2"] = io./* */;
                //d["Fork_Half_Left2"] = io./* */;
                //d["Fork_Half_Right2"] = io./* */;
                //d["Fork_End_Left2"] = io./* */;
                //d["Fork_End_Right2"] = io./* */;
                //d["Fork_Staion_Left2"] = io./* */;
                //d["Fork_Staion_Right2"] = io./* */;
                d["Double_Storage_Left1"] = io.DSTL1;
                d["Double_Storage_Right1"] = io.DSTR1;
                d["Double_Storage_L_Ex1"] = io.DSTLe1;
                d["Double_Storage_R_Ex1"] = io.DSTRe1;
                d["O_DoubleStorage_Left1"] = io.ODSTL1;
                d["O_DoubleStorage_Right1"] = io.ODSTR1;
                d["Double_Storage_L_R1"] = io.DSTLR1;
                d["Double_Storage_R_R1"] = io.DSTRR1;
                //d["Double_Storage_Left2"] = io./* */;
                //d["Double_Storage_Right2"] = io./* */;
                //d["Double_Storage_L_Ex2"] = io./* */;
                //d["Double_Storage_R_Ex2"] = io./* */;
                //d["O_DoubleStorage_Left2"] = io./* */;
                //d["O_DoubleStorage_Right2"] = io./* */;
                //d["Double_Storage_L_R2"] = io./* */;
                //d["Double_Storage_R_R2"] = io./* */;
                //d["LiDAR1_Observe_Signal"] = io./* */;
                //d["LiDAR1_Alert_Signal"] = io./* */;
                //d["LiDAR1_Alarm_Signal"] = io./* */;
                //d["LiDAR1_System_Alarm"] = io./* */;
                //d["LiDAR2_Observe_Signal"] = io./* */;
                //d["LiDAR2_Alert_Signal"] = io./* */;
                //d["LiDAR2_Alarm_Signal"] = io./* */;
                //d["LiDAR2_System_Alarm"] = io./* */;
                //d["LiDAR3_Observe_Signal"] = io./* */;
                //d["LiDAR3_Alert_Signal"] = io./* */;
                //d["LiDAR3_Alarm_Signal"] = io./* */;
                //d["LiDAR3_System_Alarm"] = io./* */;
                //d["LiDAR4_Observe_Signal"] = io./* */;
                //d["LiDAR4_Alert_Signal"] = io./* */;
                //d["LiDAR4_Alarm_Signal"] = io./* */;
                //d["LiDAR4_System_Alarm"] = io./* */;
                //d["LiDAR1_Zone01_Select"] = io./* */;
                //d["LiDAR1_Zone02_Select"] = io./* */;
                //d["LiDAR1_Zone03_Select"] = io./* */;
                //d["LiDAR1_Zone04_Select"] = io./* */;
                //d["LiDAR2_Zone01_Select"] = io./* */;
                //d["LiDAR2_Zone02_Select"] = io./* */;
                //d["LiDAR2_Zone03_Select"] = io./* */;
                //d["LiDAR2_Zone04_Select"] = io./* */;
                //d["LiDAR3_Zone01_Select"] = io./* */;
                //d["LiDAR3_Zone02_Select"] = io./* */;
                //d["LiDAR3_Zone03_Select"] = io./* */;
                //d["LiDAR3_Zone04_Select"] = io./* */;
                //d["LiDAR4_Zone01_Select"] = io./* */;
                //d["LiDAR4_Zone02_Select"] = io./* */;
                //d["LiDAR4_Zone03_Select"] = io./* */;
                //d["LiDAR4_Zone04_Select"] = io./* */;
            }

            return d;

        }


    }
}
