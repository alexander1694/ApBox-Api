﻿using API.Catalogos;
using API.Models.Dto;
using API.Operaciones.Facturacion;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Serialization;

namespace Aplicacion.LogicaPrincipal.Validacion
{
    
    public class DecodificaFacturas
    {
        private static string pathXml = @"D:\XML-GENERADOS-CARTAPORTE\FacturaRelacionada.xml";
        public List<ImpuestoDeserealizadoDto> DecodificarFactura(FacturaEmitida facturaEmitida)
        {
            ImpuestosRTDto impuestoDto = new ImpuestosRTDto();
            //Obtenemos el contenido del XML seleccionado.
            string CadenaXML = System.Text.Encoding.UTF8.GetString(facturaEmitida.ArchivoFisicoXml);
            // System.IO.File.WriteAllText(pathXml, CadenaXML);
            //var doc = XDocument.Parse(CadenaXML);
            Comprobante oComprobante;
            XmlSerializer oSerializer = new XmlSerializer(typeof(Comprobante));
            
            List<ImpuestoDeserealizadoDto> impuestoDtoList = new List<ImpuestoDeserealizadoDto>();
            ImpuestoDeserealizadoDto impuestosDTO;
            //string versionCfdi = LeerValorXML(CadenaXML, "Version", "Comprobante");

            //string path = @"C:\Users\Alexander\Downloads\Factura_ComplementoPago_v1\FacturaA1.xml";
            //string CadenaXML = System.IO.File.ReadAllText(path);
            string versionCfdi = LeerValorXML(CadenaXML, "Version", "Comprobante");
            
            if (versionCfdi == "4.0")
            {
                using (StreamReader reader = new StreamReader(new MemoryStream(facturaEmitida.ArchivoFisicoXml), Encoding.UTF8))
                {
                    //aqui deserializamos
                    oComprobante = (Comprobante)oSerializer.Deserialize(reader);
                }
                /*using (StreamReader reader = new StreamReader(path))
                {
                    //aqui deserializamos
                    oComprobante = (Comprobante)oSerializer.Deserialize(reader);
                }*/
                decimal Base = 0;
                if (oComprobante.Impuestos != null)
                {
                    if (oComprobante.Impuestos.Traslados != null && oComprobante.Impuestos.Traslados.Length > 0)
                    {
                        
                        foreach (var ooComprobante in oComprobante.Impuestos.Traslados)
                        {
                            impuestosDTO = new ImpuestoDeserealizadoDto()
                            {
                                TipoImpuesto = "Traslado",
                                Base = ooComprobante.Base,
                                TasaOCuota = ooComprobante.TasaOCuota,
                                TipoFactor = ooComprobante.TipoFactor,
                                Impuesto = ooComprobante.Impuesto,
                                Importe = ooComprobante.Importe
                            };
                           
                            impuestoDtoList.Add(impuestosDTO);
                        }

                    }
                    if (oComprobante.Impuestos.Retenciones != null && oComprobante.Impuestos.Retenciones.Length > 0)
                    {
                        decimal baseR = 0;
                        string tipoFactorR = "";
                        decimal tasaOCuotaR = 0;
                     if(oComprobante.Conceptos !=null && oComprobante.Conceptos.Length > 0) { 
                        foreach(var oConcepto in oComprobante.Conceptos)
                            {
                                if(oConcepto.Impuestos!= null)
                                {
                                    if(oConcepto.Impuestos.Retenciones!=null && oConcepto.Impuestos.Retenciones.Length >0)
                                    {
                                        foreach(var retencion in oConcepto.Impuestos.Retenciones)
                                        {
                                            baseR = retencion.Base;
                                            tipoFactorR = retencion.TipoFactor;
                                            tasaOCuotaR = retencion.TasaOCuota;
                                        }
                                    }
                                }
                            }
                        }   
                        foreach (var ooComprobante in oComprobante.Impuestos.Retenciones)
                        {
                            impuestosDTO = new ImpuestoDeserealizadoDto()
                            {
                                TipoImpuesto = "Retencion",
                                Base = baseR,
                                TasaOCuota = tasaOCuotaR,
                                TipoFactor = tipoFactorR,
                                Impuesto = ooComprobante.Impuesto,
                                Importe = ooComprobante.Importe
                            };
                            impuestoDtoList.Add(impuestosDTO);
                        }

                    }
                }
            }
            return impuestoDtoList;
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
        /*public void DecodificarFactura(ref FacturaRecibida facturaRecibida, String pathCompleto)
        {
            var serializador = new XmlSerializer(typeof(Comprobante));
            var comprobante = (Comprobante)serializador.Deserialize(new MemoryStream(facturaRecibida.ArchivoFisicoXml));

            //Complementos
            XmlElement timbreFiscalDigitalFisico = null;
            XmlElement complementoPagosFisico = null;

            foreach (var complemento in comprobante.Complemento)
            {
                timbreFiscalDigitalFisico = complemento.Any.FirstOrDefault(p => p.OuterXml.Contains("tfd"));
                complementoPagosFisico = complemento.Any.FirstOrDefault(p => p.OuterXml.Contains("pago10"));
            }

            var timbreFiscalDigital = new TimbreFiscalDigital();
            if (timbreFiscalDigitalFisico != null)
            {
                timbreFiscalDigital = ObtenerComplemento<TimbreFiscalDigital>(timbreFiscalDigitalFisico);
            }

            var complementoPagos = new Pagos();
            if (complementoPagosFisico != null)
            {
                complementoPagos = ObtenerComplemento<Pagos>(complementoPagosFisico);
            }

            //Datos
            facturaRecibida.Fecha = Convert.ToDateTime(comprobante.Fecha);
            facturaRecibida.NoCertificado = comprobante.NoCertificado;
            facturaRecibida.TipoComprobante = comprobante.TipoDeComprobante;
            facturaRecibida.Version = comprobante.Version;
            facturaRecibida.LugarExpedicion = comprobante.LugarExpedicion;
            facturaRecibida.FormaPago = comprobante.FormaPago;
            facturaRecibida.MetodoPago = comprobante.MetodoPago;
            facturaRecibida.Serie = comprobante.Serie;
            facturaRecibida.Folio = comprobante.Folio;
            
            //Totales
            facturaRecibida.Subtotal = Convert.ToDouble(comprobante.SubTotal);
            facturaRecibida.Total = Convert.ToDouble(comprobante.Total);
            
            //Timbrado
            facturaRecibida.FechaTimbrado = Convert.ToDateTime(timbreFiscalDigital.FechaTimbrado);
            facturaRecibida.NoCertificadoSat = timbreFiscalDigital.NoCertificadoSAT;
            facturaRecibida.SelloDigitalCfdi = timbreFiscalDigital.SelloCFD;
            facturaRecibida.SelloSat = timbreFiscalDigital.SelloSAT;
            facturaRecibida.Certificado = comprobante.Certificado;
            facturaRecibida.Uuid = timbreFiscalDigital.UUID;

            facturaRecibida.Emisor = new Proveedor
            {
                RazonSocial = comprobante.Emisor.Nombre,
                Rfc = comprobante.Emisor.Rfc
            };

            facturaRecibida.Receptor = new Sucursal
            {
                Rfc = comprobante.Receptor.Rfc
            };

            try
            {
                facturaRecibida.Descuento = Convert.ToDouble(comprobante.Descuento);
            }
            catch (Exception)
            {
            }
        }*/

        #region Funciones

        private T ObtenerComplemento<T>(XmlElement element)
        {
            var serializador = new XmlSerializer(typeof(T));
            var nodo = (T)serializador.Deserialize(new XmlNodeReader(element));
            return nodo;
        }

        #endregion

    }
}
