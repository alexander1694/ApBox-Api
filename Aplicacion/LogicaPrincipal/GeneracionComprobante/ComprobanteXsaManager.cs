﻿using API.Catalogos;
using API.Enums;
using API.Models.Dto;
using API.Operaciones.ComplementosPagos;
using API.Operaciones.ComprobantesCfdi;
using API.Operaciones.Facturacion;
using Aplicacion.Context;
using MessagingToolkit.QRCode.Codec;
using Newtonsoft.Json;

using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.IO;
using System.IO.Compression;

using System.Linq;
using System.Net;
using System.Text;
using System.Text.Json;

namespace Aplicacion.LogicaPrincipal.GeneracionComprobante
{
    public class ComprobanteXsaManager
    {
        #region Variables

        private readonly AplicacionContext _db = new AplicacionContext();
        //private static string pathXml = @"D:\XML-GENERADOS-CARTAPORTE\comprobanteCFDI.xml";
        //private static string pathCer = @"C:\Users\Alexander\Downloads\CertificadoPruebas\CSD_Pruebas_CFDI_XIA190128J61.cer";
        //private static string pathKey = @"C:\Users\Alexander\Downloads\CertificadoPruebas\CSD_Pruebas_CFDI_XIA190128J61.key";
        //private static string passwordKey = "12345678a";
        private static string urlXsaPrueba = $"https://canal3.xsa.com.mx:9050";
        private static string keySucursalPrueba = "75768055-eb92-4f98-8b3a-d8feb9a1c116";
        private static string idSucursalprueba = "c11ab474b2ee5c72cd67d5bdc59ac6b1";
        private static string idTipoCfdPrueba = "10cefbcc4fe69cf255d2289790d64a3f";


        #endregion
        public string GenerarComprobanteCfdi(int sucursalId, int comprobanteId)
        {

            var sucursal = _db.Sucursales.Find(sucursalId);
            var comprobanteCfdi = _db.ComprobantesCfdi.Find(comprobanteId);
            string cfdiUrl = null;
            DataResponseXsaDto data = new DataResponseXsaDto();
            try
            {
                //Genera cfdi y obten url download files
                data = GetUrlDowmloadFile(comprobanteCfdi, sucursalId);

                //obten XML byte
                byte[] xml = GetByteCfdiGenerado(data,sucursal);

                //guardar facturaEmitida 
                int idFacturaEmitida =GuardarComprobante(data, comprobanteCfdi, sucursalId, xml);

                //Incrementar Folio de Sucursal segun tipo Comprobante
                if (comprobanteCfdi.TipoDeComprobante == c_TipoDeComprobante.I) { sucursal.FolioIngreso += 1; }
                if (comprobanteCfdi.TipoDeComprobante == c_TipoDeComprobante.E) { sucursal.FolioEgreso += 1; }

                _db.Entry(sucursal).State = EntityState.Modified;

                _db.SaveChanges();

                //marca facturado
                if (idFacturaEmitida > 0)
                {
                    MarcarFacturado(comprobanteCfdi.Id, idFacturaEmitida);
                }
            }
            catch (Exception ex)
            {
                throw new Exception(String.Format("Error al momento de generar el comprobante: {0}", ex.Message));

            }

            return cfdiUrl;

        }

        public DataResponseXsaDto GetUrlDowmloadFile(ComprobanteCfdi comprobanteCfdi, int sucursalId)
        {
            string response = null;
            List<String> dataObject = new List<String>();
            DataResponseXsaDto data = new DataResponseXsaDto();
            var sucursal = _db.Sucursales.Find(sucursalId);
            
             if (comprobanteCfdi.Generado == false) {
                 response = GeneraLineaString(comprobanteCfdi, sucursal);
                if(response == null) { throw new Exception("Error response : null"); }
                //deserealizamos el json response
                data = JsonConvert.DeserializeObject<DataResponseXsaDto>(response);
                

            }
            else
            {
                throw new Exception("CFDI ya se encuentra Timbrado");
            }

            //

            return data;
        }

        public string GeneraLineaString(ComprobanteCfdi comprobanteCfdi, Sucursal sucursal)
        {
            string data = "";
            int folio = 0;
            string serie = "";
            var path = "";
            List<String> ListDataObject = new List<String>();
            //path txt
            if (comprobanteCfdi.TipoDeComprobante == API.Enums.c_TipoDeComprobante.I)
            {

                path = String.Format(AppDomain.CurrentDomain.BaseDirectory + "//Content//FileTxt//{0}-{1}-{2}.txt", sucursal.SerieIngreso, sucursal.FolioIngreso, DateTime.Now.ToString("yyyyMMddHHmmssfff"));
            }
            if (comprobanteCfdi.TipoDeComprobante == API.Enums.c_TipoDeComprobante.E)
            {
                path = String.Format(AppDomain.CurrentDomain.BaseDirectory + "//Content//FileTxt//{0}-{1}-{2}.txt", sucursal.SerieIngreso, sucursal.FolioEgreso, DateTime.Now.ToString("yyyyMMddHHmmssfff"));
            }

            if(path!="" && !File.Exists(path)) { File.Create(path).Close(); }
            TextWriter tw = new StreamWriter(path,true);
            //data 01
            if (comprobanteCfdi.TipoDeComprobante == API.Enums.c_TipoDeComprobante.E)
            {
                folio = sucursal.FolioEgreso;
                serie = sucursal.SerieEgreso;
            }

            if (comprobanteCfdi.TipoDeComprobante == API.Enums.c_TipoDeComprobante.I)
            {
                folio = sucursal.FolioIngreso;
                serie = sucursal.SerieIngreso;
            }

            // formatear fecha
            var metodoPago = comprobanteCfdi.MetodoPago == null ? "|" : comprobanteCfdi.MetodoPago.ToString() + "|";
            var formaPago = comprobanteCfdi.FormaPago == null ? "|" : comprobanteCfdi.FormaPago + "|";
            var condicionPago = comprobanteCfdi.CondicionesPago == null ? "|" : comprobanteCfdi.CondicionesPago + "|";
            //data 01 Comprobante
            data = "01|" + serie + folio + "|" + serie + "|" + folio + "|" + comprobanteCfdi.FechaDocumento.ToString("yyyy-MM-ddTHH:mm:ss")
                    + "|" + comprobanteCfdi.Subtotal + "|" + comprobanteCfdi.Total + "|" + comprobanteCfdi.TotalImpuestoTrasladado
                    + "|" + comprobanteCfdi.TotalImpuestoRetenidos + "|" + "0.00" + "|||" + comprobanteCfdi.Moneda.ToString()
                    + "|" + comprobanteCfdi.TipoCambio + "|" + "|" + "|" + "|" + "|" + comprobanteCfdi.TipoDeComprobante.ToString()
                    + "|" + metodoPago + sucursal.CodigoPostal + "|"+"|" + formaPago + condicionPago + comprobanteCfdi.ExportacionId
                    + "|" + "|";
            
            tw.WriteLine(data);
            
            //data 02A CfdiRelacionado
            if(comprobanteCfdi.UUIDCfdiRelacionado != null)
            {
                data = "02A|" + comprobanteCfdi.TipoRelacion + "|" + comprobanteCfdi.UUIDCfdiRelacionado;
                tw.WriteLine(data);
                data = "";

            }

            //data 03 Receptor
            /*data = "03|" + comprobanteCfdi.Receptor.Rfc +"|" + comprobanteCfdi.Receptor.Rfc + "|" + comprobanteCfdi.Receptor.RazonSocial
                    +"|"+"|"+"|"+"|"+"|"+"|"+"|"+"|"+"|"+"|"+comprobanteCfdi.Receptor.CodigoPostal +"|" +"|" 
                    + "|"+ comprobanteCfdi.Receptor.UsoCfdi.ToString() + "|"+comprobanteCfdi.Receptor.RegimenFiscal.ToString();*/

            //receptorPrueba
            data = "03|" + "IIN970122K30" + "|" + "IIN970122K30" + "|" + "ICA INFRAESTRUCTURA"
                    + "|" + "|" + "|" + "|" + "|" + "|" + "|" + "|" + "|" + "|" + "03800" + "|" + "|"
                    + "|" + "G01" + "|" + "601";
            if (data != "")
            {
                tw.WriteLine(data);
            }

            //data 05 conceptos
            string DTraslado = "";
            string DRetencion = "";
            var DImpuestoTList = new List<string>();
            var DImpuestoRList = new List<string>();
            Random random = new Random();
            string randomNumber = ""; 
            var conceptos = _db.Conceptos.Where(c=> c.Comprobante_Id == comprobanteCfdi.Id).ToList();
            if(conceptos.Count > 0)
            {
                foreach(var concepto in conceptos)
                {
                    randomNumber = random.Next(0, 1000000).ToString("D6");
                    data = "05|"+concepto.ClaveProdServCP+"|"+(concepto.NoIdentificacion == null ? "|":concepto.NoIdentificacion+"|")+concepto.Cantidad
                            +"|"+concepto.Descripcion+"|"+concepto.ValorUnitario+"|"+concepto.Importe+"|"+(concepto.Unidad == null ? "|" : concepto.Unidad+"|")
                            +concepto.ClaveUnidad+"|"+"0|"+randomNumber +"|"+concepto.ObjetoImpuestoId;
                    
                    tw.WriteLine(data);
                   
                    //data 05C Traslado
                    if (concepto.Traslado_Id != null)
                    {
                        DTraslado = "05C|" + randomNumber + "|" + concepto.Traslado.Base + "|"+concepto.Traslado.Impuesto+"|"+concepto.Traslado.TipoFactor.ToString()
                                    +"|"+concepto.Traslado.TasaOCuota.ToString("0.000000")+"|"+concepto.Traslado.Importe;
                        //tw.WriteLine(DTraslado);
                        DImpuestoTList.Add(DTraslado);
                        
                    }
                    //data 05D Retencion
                    if (concepto.Retencion_Id!= null)
                    {
                        DRetencion = "05D|"+randomNumber+"|"+concepto.Retencion.Base+"|"+concepto.Retencion.Impuesto+"|"+concepto.Retencion.TipoFactor.ToString()
                                      +"|"+concepto.Retencion.TasaOCuota.ToString("0.000000") + "|"+concepto.Retencion.Importe;
                        //tw.WriteLine(DRetencion);
                        DImpuestoRList.Add(DRetencion);
                    }
                }
                data = "";
                DTraslado = "";
                DRetencion = "";
            }
            if(DImpuestoTList.Count > 0)
            {
                foreach(var Traslado in DImpuestoTList) { tw.WriteLine(Traslado);}
            }
            if(DImpuestoRList.Count > 0)
            {
                foreach(var Retencion in DImpuestoRList) { tw.WriteLine(Retencion); }
            }
            //data impuestos
            decimal sumImportT = 0;
            decimal sumImportR = 0;
            
            
            if (conceptos.Count > 0)
            {
                //Impuesto Traslados
                var TConcepto = conceptos.Where(c => c.Traslado_Id != null).ToList();

                //Tasa
                var TTasaIsr = TConcepto.Where(t => t.Traslado.Impuesto == "001" && t.Traslado.TipoFactor == API.Enums.CartaPorteEnums.c_TipoFactor.Tasa).ToList();
                if(TTasaIsr.Count > 0) { sumImportT = 0; DTraslado = ""; TTasaIsr.ForEach(tt => sumImportT += tt.Traslado.Importe);

                    TTasaIsr.ForEach(t => DTraslado = "06|" + t.Traslado.Impuesto + "|" + t.Traslado.TasaOCuota.ToString("0.000000")
                                      + "|" + sumImportT+ "|" + t.Traslado.TipoFactor + "|" + t.Traslado.Base);
                    
                    tw.WriteLine(DTraslado);
                    
                }
                
                var TTasaIva = TConcepto.Where(t => t.Traslado.Impuesto == "002" && t.Traslado.TipoFactor == API.Enums.CartaPorteEnums.c_TipoFactor.Tasa).ToList();
               if(TTasaIva.Count > 0) { sumImportT = 0; DTraslado = ""; TTasaIva.ForEach(tt => sumImportT += tt.Traslado.Importe);
                    TTasaIva.ForEach(t => DTraslado = "06|" + t.Traslado.Impuesto + "|" + t.Traslado.TasaOCuota.ToString("0.000000")
                                      + "|" + sumImportT + "|" + t.Traslado.TipoFactor + "|" + t.Traslado.Base);
                    tw.WriteLine(DTraslado);
                    
                }

                var TTasaIeps = TConcepto.Where(t => t.Traslado.Impuesto == "003" && t.Traslado.TipoFactor == API.Enums.CartaPorteEnums.c_TipoFactor.Tasa).ToList();
                if (TTasaIeps.Count > 0) { sumImportT = 0; DTraslado = ""; TTasaIeps.ForEach(tt => sumImportT += tt.Traslado.Importe);
                    TTasaIeps.ForEach(t => DTraslado = "06|" + t.Traslado.Impuesto + "|" + t.Traslado.TasaOCuota.ToString("0.000000")
                                      + "|" + sumImportT + "|" + t.Traslado.TipoFactor + "|" + t.Traslado.Base);
                    tw.WriteLine(DTraslado);
                    
                }
                //Cuota
                var TCuotaIsr = TConcepto.Where(t => t.Traslado.Impuesto == "001" && t.Traslado.TipoFactor == API.Enums.CartaPorteEnums.c_TipoFactor.Cuota).ToList();
                if (TCuotaIsr.Count > 0) { sumImportT = 0; DTraslado = ""; TCuotaIsr.ForEach(tt => sumImportT += tt.Traslado.Importe);
                    TCuotaIsr.ForEach(t => DTraslado = "06|" + t.Traslado.Impuesto + "|" + t.Traslado.TasaOCuota.ToString("0.000000")
                                      + "|" + sumImportT + "|" + t.Traslado.TipoFactor + "|" + t.Traslado.Base);
                    tw.WriteLine(DTraslado);
                    
                }

                var TCuotaIva = TConcepto.Where(t => t.Traslado.Impuesto == "002" && t.Traslado.TipoFactor == API.Enums.CartaPorteEnums.c_TipoFactor.Cuota).ToList();
                if (TCuotaIva.Count > 0) { sumImportT = 0; DTraslado = ""; TCuotaIva.ForEach(tt => sumImportT += tt.Traslado.Importe);
                    TCuotaIva.ForEach(t => DTraslado = "06|" + t.Traslado.Impuesto + "|" + t.Traslado.TasaOCuota.ToString("0.000000")
                                      + "|" + sumImportT + "|" + t.Traslado.TipoFactor + "|" + t.Traslado.Base);
                    tw.WriteLine(DTraslado);
                    
                }

                var TCuotaIeps = TConcepto.Where(t => t.Traslado.Impuesto == "003" && t.Traslado.TipoFactor == API.Enums.CartaPorteEnums.c_TipoFactor.Cuota).ToList();
                if (TCuotaIeps.Count > 0) { sumImportT = 0; DTraslado = ""; TCuotaIeps.ForEach(tt => sumImportT += tt.Traslado.Importe);
                    TCuotaIeps.ForEach(t => DTraslado = "06|" + t.Traslado.Impuesto + "|" + t.Traslado.TasaOCuota.ToString("0.000000")
                                      + "|" + sumImportT + "|" + t.Traslado.TipoFactor + "|" + t.Traslado.Base);
                    tw.WriteLine(DTraslado);
                    
                }
                //Exento
                var TExentoIsr = TConcepto.Where(t => t.Traslado.Impuesto == "001" && t.Traslado.TipoFactor == API.Enums.CartaPorteEnums.c_TipoFactor.Exento).ToList();
                if (TExentoIsr.Count > 0) { sumImportT = 0; DTraslado = ""; TExentoIsr.ForEach(tt => sumImportT += tt.Traslado.Importe);
                    TExentoIsr.ForEach(t => DTraslado = "06|" + t.Traslado.Impuesto + "|" + t.Traslado.TasaOCuota.ToString("0.000000")
                                      + "|" + sumImportT + "|" + t.Traslado.TipoFactor + "|" + t.Traslado.Base);
                    tw.WriteLine(DTraslado);
                    
                }

                var TExentoIva = TConcepto.Where(t => t.Traslado.Impuesto == "002" && t.Traslado.TipoFactor == API.Enums.CartaPorteEnums.c_TipoFactor.Exento).ToList();
                if (TExentoIva.Count > 0) { sumImportT = 0; DTraslado = ""; TExentoIva.ForEach(tt => sumImportT += tt.Traslado.Importe);
                    TExentoIva.ForEach(t => DTraslado = "06|" + t.Traslado.Impuesto + "|" + t.Traslado.TasaOCuota.ToString("0.000000")
                                      + "|" + sumImportT + "|" + t.Traslado.TipoFactor + "|" + t.Traslado.Base);
                    tw.WriteLine(DTraslado);
                    
                }

                var TExentoIeps = TConcepto.Where(t => t.Traslado.Impuesto == "003" && t.Traslado.TipoFactor == API.Enums.CartaPorteEnums.c_TipoFactor.Exento).ToList();
                if (TExentoIeps.Count > 0) { sumImportT = 0; DTraslado = ""; TExentoIeps.ForEach(tt => sumImportT += tt.Traslado.Importe);
                    TExentoIeps.ForEach(t => DTraslado = "06|" + t.Traslado.Impuesto + "|" + t.Traslado.TasaOCuota.ToString("0.000000")
                                      + "|" + sumImportT + "|" + t.Traslado.TipoFactor + "|" + t.Traslado.Base);
                    tw.WriteLine(DTraslado);
                    
                }

                
                // Impuesto Retenciones
                //Tasa
                var RConcepto = conceptos.Where(c => c.Retencion_Id != null).ToList();
                var RTasaIsr = RConcepto.Where(t => t.Retencion.Impuesto == "001" && t.Retencion.TipoFactor == API.Enums.CartaPorteEnums.c_TipoFactor.Tasa).ToList();
                if (RTasaIsr.Count > 0) { sumImportR = 0; DRetencion = ""; RTasaIsr.ForEach(tt => sumImportR += tt.Retencion.Importe);
                    RTasaIsr.ForEach(t => DRetencion = "07|" + t.Traslado.Impuesto + "|" + sumImportR);
                    tw.WriteLine(DRetencion);
                    
                }

                var RTasaIva = RConcepto.Where(t => t.Retencion.Impuesto == "002" && t.Retencion.TipoFactor == API.Enums.CartaPorteEnums.c_TipoFactor.Tasa).ToList();
                if (RTasaIva.Count > 0)
                {
                    sumImportR = 0; DRetencion = ""; RTasaIva.ForEach(tt => sumImportR += tt.Retencion.Importe);
                    RTasaIva.ForEach(t => DRetencion = "07|" + t.Traslado.Impuesto + "|" + sumImportR);
                    tw.WriteLine(DRetencion);
                    
                }

                var RTasaIeps = RConcepto.Where(t => t.Retencion.Impuesto == "003" && t.Retencion.TipoFactor == API.Enums.CartaPorteEnums.c_TipoFactor.Tasa).ToList();
                if (RTasaIeps.Count > 0)
                {
                    sumImportR = 0; DRetencion = ""; RTasaIeps.ForEach(tt => sumImportR += tt.Retencion.Importe);
                    RTasaIeps.ForEach(t => DRetencion = "07|" + t.Traslado.Impuesto + "|" + sumImportR);
                    tw.WriteLine(DRetencion);
                    
                }

                //cuota
                var RCuotaIsr = RConcepto.Where(t => t.Retencion.Impuesto == "001" && t.Retencion.TipoFactor == API.Enums.CartaPorteEnums.c_TipoFactor.Cuota).ToList();
                if (RCuotaIsr.Count > 0)
                {
                    sumImportR = 0; DRetencion = ""; RCuotaIsr.ForEach(tt => sumImportR += tt.Retencion.Importe);
                    RCuotaIsr.ForEach(t => DRetencion = "07|" + t.Traslado.Impuesto + "|" + sumImportR);
                    tw.WriteLine(DRetencion);
                    
                }

                var RCuotaIva = RConcepto.Where(t => t.Retencion.Impuesto == "002" && t.Retencion.TipoFactor == API.Enums.CartaPorteEnums.c_TipoFactor.Cuota).ToList();
                if (RCuotaIva.Count > 0)
                {
                    sumImportR = 0; DRetencion = ""; RCuotaIva.ForEach(tt => sumImportR += tt.Retencion.Importe);
                    RCuotaIva.ForEach(t => DRetencion = "07|" + t.Traslado.Impuesto + "|" + sumImportR);
                    tw.WriteLine(DRetencion);
                    
                }

                var RCuotaIeps = RConcepto.Where(t => t.Retencion.Impuesto == "003" && t.Retencion.TipoFactor == API.Enums.CartaPorteEnums.c_TipoFactor.Cuota).ToList();
                if (RCuotaIeps.Count > 0)
                {
                    sumImportR = 0; DRetencion = ""; RCuotaIeps.ForEach(tt => sumImportR += tt.Retencion.Importe);
                    RCuotaIeps.ForEach(t => DRetencion = "07|" + t.Traslado.Impuesto + "|" + sumImportR);
                    tw.WriteLine(DRetencion);
                    
                }

                //Exento
                var RExentoIsr = RConcepto.Where(t => t.Retencion.Impuesto == "001" && t.Retencion.TipoFactor == API.Enums.CartaPorteEnums.c_TipoFactor.Cuota).ToList();
                if (RExentoIsr.Count > 0)
                {
                    sumImportR = 0; DRetencion = ""; RExentoIsr.ForEach(tt => sumImportR += tt.Retencion.Importe);
                    RExentoIsr.ForEach(t => DRetencion = "07|" + t.Traslado.Impuesto + "|" + sumImportR);
                    tw.WriteLine(DRetencion);
                    
                }

                var RExentoIva = RConcepto.Where(t => t.Retencion.Impuesto == "002" && t.Retencion.TipoFactor == API.Enums.CartaPorteEnums.c_TipoFactor.Cuota).ToList();
                if (RExentoIva.Count > 0)
                {
                    sumImportR = 0; DRetencion = ""; RExentoIva.ForEach(tt => sumImportR += tt.Retencion.Importe);
                    RExentoIva.ForEach(t => DRetencion = "07|" + t.Traslado.Impuesto + "|" + sumImportR);
                    tw.WriteLine(DRetencion);
                    
                }

                var RExentoIeps = RConcepto.Where(t => t.Retencion.Impuesto == "003" && t.Retencion.TipoFactor == API.Enums.CartaPorteEnums.c_TipoFactor.Cuota).ToList();
                if (RExentoIeps.Count > 0)
                {
                    sumImportR = 0; DRetencion = ""; RExentoIeps.ForEach(tt => sumImportR += tt.Retencion.Importe);
                    RExentoIeps.ForEach(t => DRetencion = "07|" + t.Traslado.Impuesto + "|" + sumImportR);
                    tw.WriteLine(DRetencion);
                    
                }


            }
            tw.Close();
            //envio de datos para generar json
            string response = RequestGeneracionCfdi(sucursal, folio, serie, path);
                
            return response;
        }

        public string RequestGeneracionCfdi(Sucursal sucursal,int folio,string serie, string path)
        {
            var lineString = "";
            string responseBody = null;
            //URL prueba
            var urlXsa = urlXsaPrueba+ "/" + keySucursalPrueba + "/cfdis";
            //URL produccion
            //var urlXsa = $"https://" + sucursal.Servidor + "/" + keySucursalPrueba + "/cfdis";
            if(sucursal.TipoCfdXsa == null) { throw new Exception("Error: Tipo CFD XSA NULL"); }
            if(sucursal.IdSucursalXsa == null) { throw new Exception("Error: Id Sucursal XSA NULL"); }
            var request = (HttpWebRequest)WebRequest.Create(urlXsa);
            request.Method = "PUT";
            request.ContentType = "application/json";
            request.Accept = "application/json";

            //nombre del archivo txt
            string nameFile = String.Format("{0}{1}_{2}.txt", serie, folio, sucursal.Rfc);
            //obtener data txt
            string[] lines = File.ReadAllLines(path);
            foreach (string line in lines)
            {
                lineString += line +"\n";
            }

            //llenamos el objeto 
            var dataJsonXsa = new DataJsonXsaDto()
            {
                idSucursal = idSucursalprueba,
                idTipoCfd = idTipoCfdPrueba,
                nombre = nameFile,
                archivoFuente = lineString
            };

            //serealizamos json
             string jsonString = System.Text.Json.JsonSerializer.Serialize(dataJsonXsa);

            File.Delete(path);
            //enviaamos request
            using (var streamWriter = new StreamWriter(request.GetRequestStream()))
            {
                streamWriter.Write(jsonString);
                streamWriter.Flush();
                streamWriter.Close();
            }
            try
            {
                using (WebResponse response = request.GetResponse())
                {
                    using (Stream strReader = response.GetResponseStream())
                    {
                        if (strReader == null) { throw new Exception("Error al momento de obtener el response: null"); };
                        using (StreamReader objReader = new StreamReader(strReader))
                        {
                            responseBody = objReader.ReadToEnd();
                            
                        }
                    }
                }
            }
            catch (WebException ex)
            {
                string message = "";
                using (WebResponse response = ex.Response)
                {
                    HttpWebResponse httpResponse = (HttpWebResponse)response;
                    message =  httpResponse.StatusCode.ToString();
                    using (Stream data = response.GetResponseStream())
                    using (var reader = new StreamReader(data))
                    {
                         message  +=  ":"+reader.ReadToEnd();
                    }
                }
                    throw new Exception("Error: " +message);
            }

            return responseBody;
        }

        public byte[] GetByteCfdiGenerado(DataResponseXsaDto data,Sucursal sucursal)
        {
            byte[] xml = new byte[1024];
            //url api xsa dowload Files prueba
            var url = urlXsaPrueba + data.xmlDownload;
            //url produccion
            //var url = $"https://" + sucursal.Servidor + data.xmlDownload;
            var request = (HttpWebRequest)WebRequest.Create(url);
            request.Method = "GET";
            

            try
            {
                //get byte response
                WebResponse response = request.GetResponse();
                MemoryStream ms = new MemoryStream();
                response.GetResponseStream().CopyTo(ms);

                xml = ms.ToArray();
                    
                
            }
            catch (WebException ex)
            {
                throw new Exception(ex.Message);
            }
            return xml;
        }

        private int GuardarComprobante(DataResponseXsaDto data, ComprobanteCfdi comprobanteCfdi, int sucursalId,byte[] xml)
        {
            var utf8 = new UTF8Encoding();
            var facturaInternaEmitida = new FacturaEmitida
            {
                ComplementosPago = new List<ComplementoPago>(),
                EmisorId = sucursalId,
                ReceptorId = comprobanteCfdi.ReceptorId,
                Fecha = comprobanteCfdi.FechaDocumento,
                Folio = data.folio.ToString(),
                Moneda = (c_Moneda)comprobanteCfdi.Moneda,
                Serie = data.serie,
                Subtotal = (double)comprobanteCfdi.Subtotal,
                TipoCambio = Convert.ToDouble(comprobanteCfdi.TipoCambio),
                TipoComprobante = comprobanteCfdi.TipoDeComprobante,
                Total = Decimal.ToDouble(comprobanteCfdi.Total),
                Uuid = data.uuid,
                ArchivoFisicoXml = xml,
                CodigoQR = GeneraQR(comprobanteCfdi,data),
                Status = API.Enums.Status.Activo
            };
            if (comprobanteCfdi.FormaPago != null)
            {
                facturaInternaEmitida.FormaPago = comprobanteCfdi.FormaPago;
            }
            else { facturaInternaEmitida.FormaPago = null; }
            if (comprobanteCfdi.MetodoPago != null)
            {
                facturaInternaEmitida.MetodoPago = (c_MetodoPago)Enum.ToObject(typeof(c_MetodoPago), comprobanteCfdi.MetodoPago);
            }
            else { facturaInternaEmitida.MetodoPago = null; }

            _db.FacturasEmitidas.Add(facturaInternaEmitida);
            _db.SaveChanges();

            return facturaInternaEmitida.Id;
        }

        public byte[] GeneraQR(ComprobanteCfdi comprobante,DataResponseXsaDto data)
        {
            //string URL
            string str1 = string.Format(AppDomain.CurrentDomain.BaseDirectory + "//Content//Temp//QR-{0}.jpg", (object)DateTime.Now.ToString("yyyyMMddHHmmssffff"));
            //string data
            string str2 = string.Format("?re={0}&rr={1}&tt={2:0000000000.000000}&id={3}", (object)comprobante.Sucursal.Rfc, (object)comprobante.Receptor.Rfc,
                (object)comprobante.Total, (object)data.uuid);
            //genera QR Temp
            new QRCodeEncoder().Encode(str2).Save(str1);

            //get byte imagen
            byte[] imgdata = System.IO.File.ReadAllBytes(str1);

            return imgdata;
        }

        private void MarcarFacturado(int comprobanteId, int facturaEmitidaId)
        {
            var comprobanteCfdi = _db.ComprobantesCfdi.Find(comprobanteId);
            comprobanteCfdi.FacturaEmitidaId = facturaEmitidaId;
            comprobanteCfdi.Generado = true;
            _db.Entry(comprobanteCfdi).State = EntityState.Modified;
            _db.SaveChanges();
        }

        public List<DataCancelacionResponseXsaDto> Cancelar(ComprobanteCfdi comprobanteCfdi)
        {
            var comprobante = _db.ComprobantesCfdi.Find(comprobanteCfdi.Id);
            var sucursal = _db.Sucursales.Find(comprobante.SucursalId);

            string responseBody = "";
            List<DataCancelacionResponseXsaDto> dataXsa = new List<DataCancelacionResponseXsaDto>();
            //url prueba
            var urlCancelacion = urlXsaPrueba + "/" + keySucursalPrueba + "/cfdis/cancelar";
            //url produccion
            //var urlCancelacion = $"https://" + sucursal.Servidor + "/" + sucursal.KeyXsa + "/cfdis/cancelar";

            //Obtenemos el contenido del XML seleccionado.
            string CadenaXML = System.Text.Encoding.UTF8.GetString(comprobante.FacturaEmitida.ArchivoFisicoXml);
            string UUID = LeerValorXML(CadenaXML, "UUID", "TimbreFiscalDigital");
            if (UUID == "")
            {
                throw new Exception(String.Format("El CFDI seleccionado no está timbrado."));

            }

            //
            string folioSustitucion = (comprobanteCfdi.FolioSustitucion == null ? "" : comprobanteCfdi.FolioSustitucion);

            //conexion web service XSA
            var request = (HttpWebRequest)WebRequest.Create(urlCancelacion);
            request.Method = "POST";
            request.ContentType = "application/json";
            request.Accept = "application/json";
            //genera json

            //llenamos el objeto 
            var dataCancelacion = new DataCancelacionRequestXsaDto()
            {
                motivo = comprobanteCfdi.MotivoCancelacion,
                folioSustitucion = folioSustitucion,
                uuid = new List<String> {
                   UUID
                }
            };

            //serealizamos json
            string jsonString = System.Text.Json.JsonSerializer.Serialize(dataCancelacion);

            //envio de la peticion request
            using (var streamWriter = new StreamWriter(request.GetRequestStream()))
            {
                streamWriter.Write(jsonString);
                streamWriter.Flush();
                streamWriter.Close();
            }

            try
            {
                using (WebResponse response = request.GetResponse())
                {
                    using (Stream strReader = response.GetResponseStream())
                    {
                        if (strReader == null) { throw new Exception("Error al momento de obtener el response: null"); };
                        using (StreamReader objReader = new StreamReader(strReader))
                        {
                            responseBody = objReader.ReadToEnd();

                        }
                    }
                }
            }
            catch (WebException ex)
            {
                string message = "";
                using (WebResponse response = ex.Response)
                {
                    HttpWebResponse httpResponse = (HttpWebResponse)response;
                    message = httpResponse.StatusCode.ToString();
                    using (Stream data = response.GetResponseStream())
                    using (var reader = new StreamReader(data))
                    {
                        message += ":" + reader.ReadToEnd();
                    }
                }
                throw new Exception("Error: " + message);
            }
            //deserealizamos el json response
            dataXsa = JsonConvert.DeserializeObject<List<DataCancelacionResponseXsaDto>>(responseBody);

            //marca cancelacion
            string status = null;
            dataXsa.ForEach(d => status = d.status);
            if (status == "201" || status == "202")
            {
                MarcarNoFacturado(comprobante.Id);
            }
            return dataXsa;
        }

        public string LeerValorXML(string xml, string atributo, string nodo)
        {
            //Variables
            string valor;
            int inicio = 0;
            int fin = 0;
            int indexNodo = 0;
            atributo = " " + atributo + "=\"";
            if (!string.IsNullOrEmpty(nodo))
            {
                indexNodo = xml.IndexOf(nodo);
            }
            if (indexNodo < 0)
                return "";
            //Buscamos y leemos el nombre del atributo
            inicio = xml.IndexOf(atributo, indexNodo) + atributo.Length;
            if (inicio < atributo.Length)
            {
                return "";
            }
            fin = xml.IndexOf("\"", inicio);
            valor = xml.Substring(inicio, fin - inicio);
            //Regreso de valores si encontro
            return valor;
        }

        private void MarcarNoFacturado(int comprobanteId)
        {
            var comprobante = _db.ComprobantesCfdi.Find(comprobanteId);
            comprobante.Status = API.Enums.Status.Cancelado;
            _db.Entry(comprobante).State = EntityState.Modified;
            _db.SaveChanges();
        }

        public byte[] DownloadPDFXsa(int comprobanteId)
        {
            byte[] pdf = new byte[1024];
            string url = "";
            string destinationPath = null;

            try
            {
                var comprobanteCfdi = _db.ComprobantesCfdi.Find(comprobanteId);
                var sucursal = _db.Sucursales.Find(comprobanteCfdi.SucursalId);
                var path = String.Format(AppDomain.CurrentDomain.BaseDirectory + "//Content//Temp//{0}-{1}-{2}.zip", comprobanteCfdi.FacturaEmitida.Serie, comprobanteCfdi.FacturaEmitida.Folio, DateTime.Now.ToString("yyyyMMddHHmmssfff"));

                //url Xsa prueba
                url = $"https://canal3.xsa.com.mx:9050" + "/75768055-eb92-4f98-8b3a-d8feb9a1c116" + "/descargasCfdi?uuid=" + comprobanteCfdi.FacturaEmitida.Uuid + "&folio=" + comprobanteCfdi.FacturaEmitida.Folio + "&serie=" + comprobanteCfdi.FacturaEmitida.Serie + "&representacion=PDF";
                //url produccion
                //url = $"https://" + sucursal.Servidor + ":9050/" + sucursal.KeyXsa + "/descargasCfdi?uuid=" +comprobanteCfdi.FacturaEmitida.Uuid +"&folio="+comprobanteCfdi.FacturaEmitida.Folio+"&serie="+ comprobanteCfdi.FacturaEmitida.Serie+"&representacion=PDF";
                var request = (HttpWebRequest)WebRequest.Create(url);
                request.ContentType = "application/zip";
                request.Method = "GET";

                //get byte response

                HttpWebResponse response = null;
                FileStream fs = null;

                fs = File.Create(path);
                response = (HttpWebResponse)request.GetResponse();
                Stream streamResponse = response.GetResponseStream();
                byte[] buffer = new byte[1024];
                int read;
                while ((read = streamResponse.Read(buffer, 0, buffer.Length)) > 0)
                {
                    fs.Write(buffer, 0, read);
                }
                fs.Flush(); 
                fs.Close(); 
            
                //path unzip
                var pathPDF = AppDomain.CurrentDomain.BaseDirectory+"//Content//Temp//";
               
                //unzip 
               using (ZipArchive archive = ZipFile.OpenRead(path))
                {
                    foreach (ZipArchiveEntry entry in archive.Entries)
                    {
                        if (entry.FullName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
                        {
                            destinationPath = Path.GetFullPath(Path.Combine(pathPDF, entry.FullName));
                            entry.ExtractToFile(destinationPath);
                        }
                        
                    }
                    
                }
                //delete ZIP
                File.Delete(path);

                ///Get Byte pdf
                pdf = File.ReadAllBytes(destinationPath);
                File.Delete(destinationPath);

            }
            catch (Exception e)
            {
                throw new Exception(e.Message);
            }
            return pdf;
        }

        public byte[] DowloadAcuseCancelacion(ComprobanteCfdi comprobanteCfdi)
        {
            var sucursal = _db.Sucursales.Find(comprobanteCfdi.SucursalId);
            

            byte[] xmlCancelacion = new byte[1024];
            string destinationPath = null;
            try
            {
                var path = String.Format(AppDomain.CurrentDomain.BaseDirectory + "//Content//Temp//{0}-{1}-{2}.zip", comprobanteCfdi.FacturaEmitida.Serie, comprobanteCfdi.FacturaEmitida.Folio, DateTime.Now.ToString("yyyyMMddHHmmssfff"));

                //URL prueba
                var urlCancelacion = urlXsaPrueba + "/" + keySucursalPrueba + "/descargasCfdi?uuid=" + comprobanteCfdi.FacturaEmitida.Uuid + "&folio=" + comprobanteCfdi.FacturaEmitida.Folio + "&serie=" + comprobanteCfdi.FacturaEmitida.Serie + "&representacion=ACUSE";
                //URL produccion
                //var urlCancelacion = $"https://" + sucursal.Servidor + ":9050/" + sucursal.KeyXsa + "/descargasCfdi?uuid=" +comprobanteCfdi.FacturaEmitida.Uuid +"&folio="+comprobanteCfdi.FacturaEmitida.Folio+"&serie="+ comprobanteCfdi.FacturaEmitida.Serie+"&representacion=ACUSE";
                var request = (HttpWebRequest)WebRequest.Create(urlCancelacion);
                request.ContentType = "application/zip";
                request.Method = "GET";

                //get byte response

                HttpWebResponse response = null;
                FileStream fs = null;

                fs = File.Create(path);
                response = (HttpWebResponse)request.GetResponse();
                Stream streamResponse = response.GetResponseStream();
                byte[] buffer = new byte[1024];
                int read;
                while ((read = streamResponse.Read(buffer, 0, buffer.Length)) > 0)
                {
                    fs.Write(buffer, 0, read);
                }
                fs.Flush();
                fs.Close();

                //path unzip
                var pathXML = AppDomain.CurrentDomain.BaseDirectory + "//Content//Temp//";

                //unzip 
                using (ZipArchive archive = ZipFile.OpenRead(path))
                {
                    foreach (ZipArchiveEntry entry in archive.Entries)
                    {
                        if (entry.FullName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
                        {
                            destinationPath = Path.GetFullPath(Path.Combine(pathXML, entry.FullName));
                            entry.ExtractToFile(destinationPath);
                        }

                    }

                }
                //delete ZIP
                File.Delete(path);

                ///Get Byte xml
                xmlCancelacion = File.ReadAllBytes(destinationPath);
                File.Delete(destinationPath);

            }
            catch (Exception e)
            {
                throw new Exception(e.Message);
            }
            return xmlCancelacion;
        }
    }
    }
