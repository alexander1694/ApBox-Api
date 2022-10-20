﻿using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Web;
using System.Web.Mvc;
using APBox.Context;
using API.Models.Facturas;
using API.Operaciones.Facturacion;
using Aplicacion.LogicaPrincipal.Descargas;
using Aplicacion.LogicaPrincipal.Facturas;
using Aplicacion.LogicaPrincipal.GeneracionComplementosPagos;
using Aplicacion.LogicaPrincipal.Validacion;
using MySql.Data.MySqlClient;
using MySqlConnector;

namespace APBox.Controllers.Catalogos
{
    [APBox.Control.SessionExpire]
    public class FacturasEmitidasController : Controller
    {

        #region Variables

        private readonly APBoxContext _db = new APBoxContext();
        private readonly PagosManager _pagosManager = new PagosManager();
        private readonly OperacionesCfdisEmitidos _operacionesCfdisEmitidos = new OperacionesCfdisEmitidos();
        private readonly DescargasManager _descargasManager =  new DescargasManager();
        private readonly DecodificaFacturas _decodifica = new DecodificaFacturas();

        #endregion

        // GET: FacturasEmitidas
        public ActionResult Index()
        {
            var sucursalId = ObtenerSucursal();

            var facturasEmitidasModel = new FacturasEmitidasModel
            {
                FechaInicial = DateTime.Now.AddDays(-6), // SE RESTA 6 DIAS PARA MOSTRAR EL RANGO DE FACTURAS GENERADAS EN UN SEMANA
                FechaFinal = DateTime.Now,
                SucursalId = ObtenerSucursal(),
            };

            _operacionesCfdisEmitidos.ObtenerFacturas(ref facturasEmitidasModel);

            ViewBag.Controller = "FacturasEmitidas";
            ViewBag.Action = "Index";
            ViewBag.ActionES = "Index";
            ViewBag.NameHere = "cfdi";

            return View(facturasEmitidasModel);
        }

        [HttpPost]
        public ActionResult Index(FacturasEmitidasModel facturasEmitidasModel)
        {
            if (ModelState.IsValid)
            {
                _operacionesCfdisEmitidos.ObtenerFacturas(ref facturasEmitidasModel);
            }
            ViewBag.Controller = "FacturasEmitidas";
            ViewBag.Action = "Index";
            ViewBag.ActionES = "Index";
            ViewBag.NameHere = "cfdi";
            return View(facturasEmitidasModel);
        }

        // GET: FacturasEmitidas/Details/5
        public ActionResult Details(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            FacturaEmitida facturaEmitida = _db.FacturasEmitidas.Find(id);
            if (facturaEmitida == null)
            {
                return HttpNotFound();
            }
            return View(facturaEmitida);
        }

        // GET: FacturasEmitidas/Create
        public ActionResult Create()
        {
            var facturaEmitida = new FacturaEmitida
            {
                EmisorId = ObtenerSucursal()
            };

            return View(facturaEmitida);
        }

        // POST: FacturasEmitidas/Create
        // To protect from overposting attacks, please enable the specific properties you want to bind to, for 
        // more details see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create(FacturaEmitida facturaEmitida)
        {
            if (ModelState.IsValid)
            {
                _db.FacturasEmitidas.Add(facturaEmitida);
                _db.SaveChanges();
                return RedirectToAction("Index");
            }

            return View(facturaEmitida);
        }

        // GET: FacturasEmitidas/Edit/5
        public ActionResult Edit(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            FacturaEmitida facturaEmitida = _db.FacturasEmitidas.Find(id);
            if (facturaEmitida == null)
            {
                return HttpNotFound();
            }
            return View(facturaEmitida);
        }

        // POST: FacturasEmitidas/Edit/5
        // To protect from overposting attacks, please enable the specific properties you want to bind to, for 
        // more details see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit(FacturaEmitida facturaEmitida)
        {
            if (ModelState.IsValid)
            {
                _db.Entry(facturaEmitida).State = EntityState.Modified;
                _db.SaveChanges();
                return RedirectToAction("Index");
            }
            return View(facturaEmitida);
        }

        // GET: FacturasEmitidas/Delete/5
        public ActionResult Delete(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            FacturaEmitida facturaEmitida = _db.FacturasEmitidas.Find(id);
            if (facturaEmitida == null)
            {
                return HttpNotFound();
            }
            return View(facturaEmitida);
        }

        // POST: FacturasEmitidas/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteConfirmed(int id)
        {
            FacturaEmitida facturaEmitida = _db.FacturasEmitidas.Find(id);
            _db.FacturasEmitidas.Remove(facturaEmitida);
            _db.SaveChanges();
            return RedirectToAction("Index");
        }

        public ActionResult SubirPdf(int id)
        {
            var facturaEmitida = _db.FacturasEmitidas.Find(id);
            return View(facturaEmitida);
        }

        public ActionResult SubirPdf(FacturaEmitida facturaEmitida)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    SubeArchivos();
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError("", ex.Message);
                    return View(facturaEmitida);
                }
                return RedirectToAction("Index");
            }
            return View(facturaEmitida);
        }
        public ActionResult DescargaXML(int id)
        {
            //get xml
            var facturaEmtida = _db.FacturasEmitidas.Find(id);
            var pathCompleto = _descargasManager.GeneraFilePathXml(facturaEmtida.ArchivoFisicoXml,facturaEmtida.Serie,facturaEmtida.Folio);
            byte[] archivoFisico = System.IO.File.ReadAllBytes(pathCompleto);
            string contentType = MimeMapping.GetMimeMapping(pathCompleto);

            var cd = new System.Net.Mime.ContentDisposition
            {
                FileName = Path.GetFileName(pathCompleto),
                Inline = false,
            };
            Response.AppendHeader("Content-Disposition", cd.ToString());
            //Elimino el archivo Temp
            System.IO.File.Delete(pathCompleto);
            return File(archivoFisico, contentType);
        }

        public ActionResult DescargaPDF(int id)
        {
            //objetos version 4.0
            ComprobanteCFDI oComprobante = new ComprobanteCFDI();
            //objetos version 3.3
            ComprobanteCFDI33 oComprobante33 = new ComprobanteCFDI33();
            string tipoDocumento = null;
            byte[] archivoFisico = new byte[255];
            var facturaEmitida = _db.FacturasEmitidas.Find(id);
            //busca version del CFDI del archivo
            string CadenaXML = System.Text.Encoding.UTF8.GetString(facturaEmitida.ArchivoFisicoXml);
            string versionCfdi = _decodifica.LeerValorXML(CadenaXML, "Version", "Comprobante");
            if (versionCfdi == "3.3")
            {
                oComprobante33 = _decodifica.DeserealizarXML33(facturaEmitida.ArchivoFisicoXml);
                 tipoDocumento = _decodifica.TipoDocumentoCfdi33(facturaEmitida.ArchivoFisicoXml);
                archivoFisico = _descargasManager.GeneraPDF33(oComprobante33,tipoDocumento ,id,true);
            }
            else
            {
                oComprobante = _decodifica.DeserealizarXML40(facturaEmitida.ArchivoFisicoXml);
                tipoDocumento = _decodifica.TipoDocumentoCfdi40(facturaEmitida.ArchivoFisicoXml);
                archivoFisico = _descargasManager.GeneraPDF40(oComprobante,tipoDocumento ,id,true);
            }

            MemoryStream ms = new MemoryStream(archivoFisico, 0, 0, true, true);
            string nameArchivo = facturaEmitida.Serie + "-" + facturaEmitida.Folio + "-" + DateTime.Now.ToString("yyyyMMddHHmmssfff");
            Response.AddHeader("content-disposition", "attachment;filename= " + nameArchivo + ".pdf");
            Response.Buffer = true;
            Response.Clear();
            Response.OutputStream.Write(ms.GetBuffer(), 0, ms.GetBuffer().Length);
            Response.OutputStream.Flush();
            Response.End();


            return new FileStreamResult(Response.OutputStream, "application/pdf");
        }

        public ActionResult Cancelar(int id)
        {
            PopulaMotivoCancelacion();
            ViewBag.Error = null;
            ViewBag.Success = null;
            var facturaEmitida = _db.FacturasEmitidas.Find(id);
            return PartialView("~/Views/FacturasEmitidas/_Cancelacion.cshtml", facturaEmitida);
        }

        [HttpPost]
        public ActionResult Cancelar(FacturaEmitida facturaEmitida)
        {
            PopulaMotivoCancelacion();
            string error = null;
            var emitida = _db.FacturasEmitidas.Find(facturaEmitida.Id);
            emitida.FolioSustitucion = facturaEmitida.FolioSustitucion;
            emitida.MotivoCancelacion = facturaEmitida.MotivoCancelacion;
            try
            {
                _operacionesCfdisEmitidos.Cancelar(emitida);

            }
            catch (Exception ex)
            {
                error = ex.Message;

            }
            if (error == null)
            {
                ViewBag.Success = "Proceso de cancelación finalizado con éxito.";
                ViewBag.Error = null;
            }
            else
            {
                ViewBag.Error = error;
                ViewBag.Success = null;
            }
            return PartialView("~/Views/FacturasEmitidas/_Cancelacion.cshtml", emitida);
        }

        public ActionResult DescargarAcuse(int id)
        {
            var facturaEmitida = _db.FacturasEmitidas.Find(id);
            string xmlCancelacion = _descargasManager.DowloadAcuseCancelacion(facturaEmitida.EmisorId,facturaEmitida.ArchivoFisicoXml);
            byte[] byteXml = Encoding.UTF8.GetBytes(xmlCancelacion);
            MemoryStream ms = new MemoryStream(byteXml, 0, 0, true, true);
            string nameArchivo = facturaEmitida.Serie + "-" + facturaEmitida.Folio + "-" + DateTime.Now.ToString("yyyyMMddHHmmssfff");
            Response.AddHeader("content-disposition", "attachment;filename= " + nameArchivo + ".xml");
            Response.Buffer = true;
            Response.Clear();
            Response.OutputStream.Write(ms.GetBuffer(), 0, ms.GetBuffer().Length);
            Response.OutputStream.Flush();
            Response.End();

            return new FileStreamResult(Response.OutputStream, "application/xml");
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _db.Dispose();
            }
            base.Dispose(disposing);
        }

        #region PopulaForma

        private int ObtenerSucursal()
        {
            return Convert.ToInt32(Session["SucursalId"]);
        }

        #endregion

        #region Archivos

        private List<String> SubeArchivos()
        {
            var paths = new List<String>();
            if (Request.Files.Count > 0)
            {
                for (int i = 0; i < Request.Files.Count; i++)
                {
                    var file = Request.Files[i];

                    if (file != null && file.ContentLength > 0)
                    {
                        try
                        {

                            var fileName = Path.GetFileName(file.FileName);

                            if (Path.GetExtension(fileName) != ".xml")
                            {
                                continue;
                            }

                            var path = Path.Combine(Server.MapPath("~/Archivos/PDFs Facturas Emitidas/"), fileName);
                            file.SaveAs(path);
                            paths.Add(path);
                        }
                        catch (Exception)
                        {
                        }

                    }
                }
                return paths;
            }
            return null;
        }

        #endregion

        public ActionResult detallePago() {
            var sucursalId = ObtenerSucursal();

            var facturasEmitidasModel = new FacturasEmitidasModel
            {
                FechaInicial = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1),
                FechaFinal = DateTime.Now,
                SucursalId = ObtenerSucursal(),
            };
            
            List<search_doc_rel_fac_emi> listaComplementosPagos = new List<search_doc_rel_fac_emi>();
            _operacionesCfdisEmitidos.ObtenerFacturas(ref facturasEmitidasModel);
            bool isEmpty = facturasEmitidasModel.FacturasEmitidas.Any();
            if (isEmpty)
            {
                foreach (var facturasEmitidas in facturasEmitidasModel.FacturasEmitidas)
                {
                    search_doc_rel_fac_emi queryFacturas = queryFacturasPagadas(facturasEmitidas.Id);
                    if (queryFacturas != null && queryFacturas.FacturaEmitidaId != 0)
                    {
                        facturasEmitidas.FolioComplementoPago = queryFacturas.Folio;
                        facturasEmitidas.SerieComplementoPago = queryFacturas.Serie;
                        facturasEmitidas.FacturaComplementoPagoId = queryFacturas.Id;
                        facturasEmitidas.FacturaEmitidaPagada = true;
                    }
                    
                }
                
            }
            ViewBag.Controller = "FacturasEmitidas";
            ViewBag.Action = "detallePago";
            ViewBag.ActionES = "Detalle Pago";
            ViewBag.NameHere = "reporte";
            return View(facturasEmitidasModel);
        }

        [HttpPost]
        public ActionResult detallePago(FacturasEmitidasModel facturasEmitidasModel)
        {
            bool isEmpty;
            if (!ModelState.IsValid)
            {
                //_operacionesCfdisEmitidos.ObtenerFacturasById(ref facturasEmitidasModel);
            }
            else
            {
                _operacionesCfdisEmitidos.ObtenerFacturas(ref facturasEmitidasModel);
            }
                    isEmpty = facturasEmitidasModel.FacturasEmitidas.Any();
                    if (isEmpty)
                    {
                        foreach (var facturasEmitidas in facturasEmitidasModel.FacturasEmitidas)
                        {
                            search_doc_rel_fac_emi queryFacturas = queryFacturasPagadas(facturasEmitidas.Id);
                            if (queryFacturas != null && queryFacturas.FacturaEmitidaId != 0)
                            {
                                facturasEmitidas.FolioComplementoPago = queryFacturas.Folio;
                                facturasEmitidas.SerieComplementoPago = queryFacturas.Serie;
                                facturasEmitidas.FacturaComplementoPagoId = queryFacturas.Id;
                                facturasEmitidas.FacturaEmitidaPagada = true;
                            }
                        }
                    }
           
            return View(facturasEmitidasModel);
        }

        public search_doc_rel_fac_emi queryFacturasPagadas(int id)
        {
            var listRelTblSearch = new search_doc_rel_fac_emi();
            const string query = @"select IFNULL(cp.FacturaEmitidaId,0) as FacturaEmitidaId, fe.Folio,fe.Serie,cp.Id from ori_documentosrelacionados dr " +
                        "join ori_pagos p on(dr.PagoId = p.Id) " +
                        "join ori_complementospagos cp on(p.ComplementoPagoId = cp.Id) " +
                        "join ori_facturasemitidas fe on (cp.FacturaEmitidaId = fe.Id) " +
                        "where dr.FacturaEmitidaId in (@Id); ";

            var resultados = _db.Database.SqlQuery<search_doc_rel_fac_emi>(query,
                    new MySqlConnector.MySqlParameter { ParameterName = "@Id", MySqlDbType = MySqlConnector.MySqlDbType.String, Value = id }).FirstOrDefault();
            
            return resultados;
        }

        private void PopulaMotivoCancelacion()
        {
            List<SelectListItem> items = new List<SelectListItem>();
            items.Add(new SelectListItem { Text = "01 - Comprobante Emitido con errores con relación", Value = "01", Selected = true });
            items.Add(new SelectListItem { Text = "02 - Comprobante emitido con errores sin relacion", Value = "02" });
            items.Add(new SelectListItem { Text = "03 - No se llevo a cabo la operación", Value = "03" });
            items.Add(new SelectListItem { Text = "04 - Operación nominativa relacionada en una factura global", Value = "04" });
            ViewBag.motivoCancelacion = items;
        }
        public class search_doc_rel_fac_emi
        {
            public int FacturaEmitidaId { get; set; }

            public string Folio { get; set; }

            public string Serie { get; set; }

            public int Id { get; set; }

        }
    

    }
}
