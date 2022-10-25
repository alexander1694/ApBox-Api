﻿using System.Data.Entity;
using System.Net;
using System.Web.Mvc;
using APBox.Context;
using API.Operaciones.ComplementosPagos;
using System;
using APBox.Control;
using Aplicacion.LogicaPrincipal.Acondicionamientos.Operaciones;
using System.Web;
using Aplicacion.LogicaPrincipal.GeneracionComplementosPagos;
using API.Models.Operaciones;
using Aplicacion.LogicaPrincipal.ComplementosPagos;
using API.Enums;
using Utilerias.LogicaPrincipal;
using System.IO;
using Aplicacion.LogicaPrincipal.CargasMasivas.CSV;
using System.Collections.Generic;
using API.Models.ComplementosPagos;
using System.Linq;
using System.Text;
using System.Data.Entity.Migrations;
using API.Operaciones.Facturacion;
using Aplicacion.LogicaPrincipal.Validacion;
using API.Models.Dto;
using Aplicacion.LogicaPrincipal.GeneraPDfCartaPorte;
using API.Enums.CartaPorteEnums;

namespace APBox.Controllers.ComplementosPago
{
    [SessionExpire]
    public class ComplementosPagosController : Controller
    {

        #region Variables

        private readonly APBoxContext _db = new APBoxContext();
        private readonly PagosManager _pagosManager = new PagosManager();
        private readonly LogicaFacadeFacturas _logicaFacadeFacturas = new LogicaFacadeFacturas();
        private readonly CargarComplementosPagos _cargarComplementosPagos = new CargarComplementosPagos();
        private readonly AcondicionarComplementosPagos _acondicionarComplementosPagos = new AcondicionarComplementosPagos();
        private readonly DecodificaFacturas _decodifica = new DecodificaFacturas();
        private readonly CreationFile _creationFile = new CreationFile();

        #endregion

        // GET: Facturas
        
        public ActionResult Index()
        {
            PopulaEstatus();
            var complementosPagosModel = new ComplementosPagosModel
            {
                Mes = (Meses)(DateTime.Now.Month),
                Anio = DateTime.Now.Year
            };

            var fechaInicial = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1, 0, 0, 0);
            var fechaFinal = new DateTime(DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day, 23, 59, 59);
            complementosPagosModel.FechaInicial = fechaInicial;
            complementosPagosModel.FechaFinal = fechaFinal;


            complementosPagosModel.ComplementosPago = _logicaFacadeFacturas.Filtrar(fechaInicial, fechaFinal,false, ObtenerSucursal());
            ViewBag.Controller = "ComplementosPagos";
            ViewBag.Action = "Index";
            ViewBag.ActionES = "Index";
            ViewBag.Button = "Crear";
            ViewBag.NameHere = "emision";
            return View(complementosPagosModel);
        }

        [HttpPost]
        public ActionResult Index(ComplementosPagosModel complementosPagosModel, string actionName)
        {
            PopulaEstatus();
            ViewBag.Controller = "ComplementosPagos";
            ViewBag.Action = "Index";
            ViewBag.ActionES = "Index";
            ViewBag.Button = "Crear";
            ViewBag.NameHere = "emision";
            if (actionName == "Filtrar")
            {
                DateTime fechaI = complementosPagosModel.FechaInicial;
                DateTime fechaF = complementosPagosModel.FechaFinal;
                //var dia = DateTime.DaysInMonth(complementosPagosModel.Anio, (int)complementosPagosModel.Mes);

                var fechaInicial = new DateTime(fechaI.Year, fechaI.Month, fechaI.Day, 0, 0, 0);
                var fechaFinal = new DateTime(fechaF.Year, fechaF.Month, fechaF.Day, 23, 59, 59);
                complementosPagosModel.FechaInicial = fechaInicial;
                complementosPagosModel.FechaFinal = fechaFinal;

                complementosPagosModel.ComplementosPago = _logicaFacadeFacturas.Filtrar(fechaInicial, fechaFinal,complementosPagosModel.Estatus, ObtenerSucursal());
            }
            else if (actionName == "Timbrar")
            {
                var errores = new List<String>();
                foreach (var complementoPago in complementosPagosModel.ComplementosPago.Where(cp => cp.Seleccionado))
                {
                    try
                    {
                        //_pagosManager.GenerarComplementoPago(complementoPago.SucursalId, complementoPago.Id, "");
                    }
                    catch (Exception ex)
                    {
                        var complementoPagoInterno = _db.ComplementosPago.Find(complementoPago.Id);
                        errores.Add(String.Format("Error de generación del complemento del receptor {0} con total de montos {1:c}: {2}", complementoPagoInterno.Receptor.RazonSocial, complementoPagoInterno.Pagos.Sum(p => p.Monto), ex.Message));
                    }
                }

                if (errores.Count == 0)
                {
                    return RedirectToAction("Index");
                }
                else
                {
                    foreach (var error in errores)
                    {
                        ModelState.AddModelError("", error);
                    }
                }
            }

            return View(complementosPagosModel);
        }

        // GET: ComplementosPago/Create
        public ActionResult Create()
        {
            PopulaClientes();
            PopulaBancos(ObtenerSucursal());
            PopulaCfdiRelacionado();
            PopulaTipoRelacion();
            PopulaFormaPago();
            PopulaExportacion();
            var sucursalC = _db.Sucursales.Find(ObtenerSucursal());
            var complementoPago = new ComplementoPago
            {
                Generado = false,
                Status = Status.Activo,
                FechaDocumento = DateTime.Now,
                Mes = (Meses)Enum.ToObject(typeof(Meses), DateTime.Now.Month),
                SucursalId = ObtenerSucursal(),
                Sucursal = sucursalC,
                Version = "2.0",
                ExportacionId = "01",//No aplica
                Pago = new Pago
                {
                    FechaPago = DateTime.Now,
                    FormaPago = "03", //Transferencia Electronica de Fondos,
                    Moneda = c_Moneda.MXN,
                    TipoCambio = 1,
                    Monto = 0.0,
                }
            };

            ViewBag.Controller = "ComplementosPagos";
            ViewBag.Action = "Create";
            ViewBag.ActionES = "Crear";
            ViewBag.NameHere = "emision";
            return View(complementoPago);
        }

        // POST: ComplementosPago/Create
        // To protect from overposting attacks, please enable the specific properties you want to bind to, for 
        // more details see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create(ComplementoPago complementoPago)
        {
            ModelState.Remove("Pago.FechaPago");
            ModelState.Remove("Pago.FormaPago");
            ModelState.Remove("Pago.Moneda");
            ModelState.Remove("Pago.TipoCambio");
            ModelState.Remove("Pago.Monto");
            ModelState.Remove("Pago.ArchivoFisico");
            ModelState.Remove("Pago.ComplementoPagoId");
            ModelState.Remove("Pago.SucursalId");
            ModelState.Remove("CfdiRelacionado.FacturaEmitidaId");

            PopulaClientes(complementoPago.ReceptorId);
            PopulaBancos(ObtenerSucursal());
           
            PopulaTipoRelacion();
            PopulaFormaPago();
            PopulaExportacion();

            if (ModelState.IsValid)
            {
                _acondicionarComplementosPagos.CargaInicial(ref complementoPago);

                try
                {
                    var pagos = complementoPago.Pagos;
                    pagos.ForEach(p => p.ComplementoPago = null);

                    complementoPago.Pagos = null;
                    complementoPago.TotalesPagoImpuestoId = null;
                    complementoPago.TotalesPagosImpuestos = null;
                    complementoPago.Status = Status.Activo;
                    _db.ComplementosPago.Add(complementoPago);
                    _db.SaveChanges();

                    foreach (var pago in pagos)
                    {
                        pago.ComplementoPago = null;
                        pago.ComplementoPagoId = complementoPago.Id;
                        _db.Pagos.Add(pago);
                    }
                    _db.SaveChanges();

                    return RedirectToAction("DocumentosRelacionados", new { @id = complementoPago.Id });
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError("", ex.Message);
                }
            }

            return View(complementoPago);
        }

        // GET: ComplementosPago/Edit/5
        public ActionResult Edit(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            ComplementoPago complementoPago = _db.ComplementosPago.Find(id);
            

            if (complementoPago == null)
            {
                return HttpNotFound();
            }

            complementoPago.Pago = new Pago
            {
                FechaPago = DateTime.Now,
                FormaPago = "03",//TransferenciaElectronicaDeFondos,
                Moneda = c_Moneda.MXN,
                TipoCambio = 1,
                Monto = 0.0,
            };

            PopulaClientes(complementoPago.ReceptorId);
            PopulaBancos(ObtenerSucursal());
            
            PopulaTipoRelacion();
            PopulaFormaPago();
            PopulaExportacion();
            complementoPago.IdTipoRelacion = complementoPago.TipoRelacion;

            ViewBag.Controller = "ComplementosPagos";
            ViewBag.Action = "Edit";
            ViewBag.ActionES = "Editar";
            ViewBag.NameHere = "emision";
            return View(complementoPago);
        }

        // POST: ComplementosPago/Edit/5
        // To protect from overposting attacks, please enable the specific properties you want to bind to, for 
        // more details see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit(ComplementoPago complementoPago)
        {
            ModelState.Remove("Pago.FechaPago");
            ModelState.Remove("Pago.FormaPago");
            ModelState.Remove("Pago.Moneda");
            ModelState.Remove("Pago.TipoCambio");
            ModelState.Remove("Pago.Monto");
            ModelState.Remove("Pago.ArchivoFisico");
            ModelState.Remove("Pago.ComplementoPagoId");
            ModelState.Remove("Pago.SucursalId");
            ModelState.Remove("Mes");

            PopulaClientes(complementoPago.ReceptorId);
            PopulaBancos(ObtenerSucursal());
            
            PopulaTipoRelacion();
            PopulaFormaPago();
            PopulaExportacion();

            if (ModelState.IsValid)
            {
                ViewBag.Controller = "ComplementosPagos";
                ViewBag.Action = "Edit";
                ViewBag.ActionES = "Editar";
                ViewBag.NameHere = "emision";
                _acondicionarComplementosPagos.Pagos(complementoPago);

                complementoPago.Pagos = null;
                _db.Entry(complementoPago).State = EntityState.Modified;
                _db.SaveChanges();
                return RedirectToAction("Index");
            }
            else
            {
                //Identifica los mensaje de error
                var errors = ModelState.Values.Where(E => E.Errors.Count > 0)
                         .SelectMany(E => E.Errors)
                         .Select(E => E.ErrorMessage)
                         .ToList();
                //Identifica el campo del Required
                var modelErrors = ModelState.Where(m => ModelState[m.Key].Errors.Any());
                ModelState.AddModelError("", "Error revisar los campos requeridos");
            }
            return View(complementoPago);
        }

        // GET: ComplementosPago/Delete/5
        [Authorize(Roles = "BORRADO")]
        public ActionResult Delete(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            ComplementoPago complementoPago = _db.ComplementosPago.Find(id);
            if (complementoPago == null)
            {
                return HttpNotFound();
            }
            PopulaClientes(complementoPago.ReceptorId);
            PopulaBancos(ObtenerSucursal());
            
            PopulaTipoRelacion();
            PopulaFormaPago();
            PopulaExportacion();

            return View(complementoPago);
        }

        // POST: ComplementosPago/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteConfirmed(int id)
        {
            ComplementoPago complementoPago = _db.ComplementosPago.Find(id);
            _db.ComplementosPago.Remove(complementoPago);
            _db.SaveChanges();
            return RedirectToAction("Index");
        }

        #region Cargas Masivas

        public ActionResult Exportar()
        {
            var pathCompleto = _cargarComplementosPagos.Exportar();
            byte[] filedata = System.IO.File.ReadAllBytes(pathCompleto);
            string contentType = MimeMapping.GetMimeMapping(pathCompleto);

            var cd = new System.Net.Mime.ContentDisposition
            {
                FileName = Path.GetFileName(pathCompleto),
                Inline = false,
            };
            Response.AppendHeader("Content-Disposition", cd.ToString());
            return File(filedata, contentType);
        }

        public ActionResult Cargar()
        {
            ViewBag.Controller = "ComplementosPagos";
            ViewBag.Action = "Cargar";
            ViewBag.ActionES = "Cargar Layout";
            ViewBag.NameHere = "emision";
            var cargasComplementosModel = new CargasComplementosModel
            {
                GrupoId = ObtenerGrupo(),
                Previsualizacion = true,
                Mes = (Meses)Enum.ToObject(typeof(Meses), DateTime.Now.Month),
                Detalles = new List<ComplementoPago>()
            };

            return View(cargasComplementosModel);
        }

        [HttpPost]
        public ActionResult Cargar(CargasComplementosModel cargasComplementosModel)
        {
            ViewBag.Controller = "ComplementosPagos";
            ViewBag.Action = "Cargar";
            ViewBag.ActionES = "Cargar Layout";
            ViewBag.NameHere = "emision";
            if (ModelState.IsValid)
            {
                string archivo;
                try
                {
                    archivo = SubeArchivo(0);
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError("", String.Format("No se pudo cargar el archivo: {0}", ex.Message));
                    return View(cargasComplementosModel);
                }

                try
                {
                    cargasComplementosModel.Detalles = _cargarComplementosPagos.Importar(archivo, ObtenerSucursal(), cargasComplementosModel.Mes, cargasComplementosModel.Previsualizacion);
                    if (cargasComplementosModel.Previsualizacion)
                    {
                        ModelState.AddModelError("", "Comando realizado con éxito");
                    }
                    else
                    {
                        return RedirectToAction("Index");
                    }
                    
                }
                catch (Exception ex)
                {
                    var errores = ex.Message.Split('|');
                    foreach (var error in errores)
                    {
                        ModelState.AddModelError("", error);
                    }
                }

            }
            return View(cargasComplementosModel);
        }

        #endregion

        #region Operaciones

        public ActionResult DocumentosRelacionados(int id)
        {
            var complementoPago = _db.ComplementosPago.Find(id);

            PopulaFacturas(complementoPago.ReceptorId);
            PopulaPagos(id);
            PopulaObjetoImpuesto();
            PopulaImpuestoSat();

            complementoPago.Pago = new Pago
            {
                DocumentoRelacionado = new DocumentoRelacionado
                {
                    Moneda = c_Moneda.MXN,
                    EquivalenciaDR = 1,
                    NumeroParcialidad = 1,
                    ImporteSaldoAnterior = 0,
                    ImportePagado = 0,
                    ImporteSaldoInsoluto = 0,
                    Retencion =  new RetencionDR()
                    {
                        Base = 0,
                        TasaOCuota = 0,
                        Importe = 0
                    },
                    Traslado = new TrasladoDR()
                    {
                        Base = 0,
                        TasaOCuota = 0,
                        Importe = 0
                    }
                },
                SucursalId = ObtenerSucursal()
            };
            ViewBag.Controller = "ComplementosPagos";
            ViewBag.Action = "DocumentosRelacionados";
            ViewBag.ActionES = "DocumentosRelacionados";
            ViewBag.NameHere = "emision";
            return View(complementoPago);
        }

        [HttpPost]
        public ActionResult DocumentosRelacionados(ComplementoPago complementoP)
        {
            PopulaFacturas(complementoP.ReceptorId);
            PopulaPagos(complementoP.Id);
            PopulaObjetoImpuesto();
            PopulaImpuestoSat();

            ModelState.Remove("Receptor.Nombre");
            ModelState.Remove("Receptor.Pais");
            ModelState.Remove("Pago.DocumentoRelacionado.IdDocumento");
            ModelState.Remove("Pago.DocumentoRelacionado.ObjetoImpuesto");
            ModelState.Remove("Pago.FormaPago");

            complementoP.Pago = null;

            ComplementoPago complementoPago = _db.ComplementosPago.Find(complementoP.Id);
            complementoPago.Pago = null;
            complementoPago.DocumentosRelacionados = complementoP.DocumentosRelacionados;
            if (ModelState.IsValid)
            {
                
                _acondicionarComplementosPagos.DocumentosRelacionados(complementoPago);
                decimal totalRetencionesISR = 0;
                decimal totalRetencionesIVA = 0;
                decimal totalRetencionesIEPS = 0;
                decimal totalTrasladosBaseIVA16 = 0;
                decimal totalTrasladosImpuestoIVA16 = 0;
                decimal totalTrasladosBaseIVA8 = 0;
                decimal totalTrasladosImpuestoIVA8 = 0;
                decimal totalTrasladosBaseIVA0 = 0;
                decimal totalTrasladosImpuestoIVA0 = 0;
                decimal totalTrasladosBaseIVAExento = 0;
                double equivalencia = 1;
                if (complementoPago.DocumentosRelacionados != null)
                {
                    foreach (var DRelacionado in complementoPago.DocumentosRelacionados)
                    {
                        //calcula EquivalenciaDR
                        /*if(DRelacionado.PagoId > 0){
                            Pago p= _db.Pagos.Find(DRelacionado.PagoId);
                            if(p != null) {
                                if(p.Moneda == DRelacionado.Moneda) { equivalencia = 1; DRelacionado.EquivalenciaDR = equivalencia; }
                                if(p.Moneda != DRelacionado.Moneda)
                                {
                                    //CALCULA EQUIVALENCIA 
                                    equivalencia = ((double)(DRelacionado.ImportePagado / p.Monto));
                                    //PARSEO A 6 DECIMALES
                                    equivalencia = Math.Truncate(equivalencia * 1000000) / 1000000;
                                    DRelacionado.EquivalenciaDR = equivalencia;
                                }
                            }
                        
                        }*/
                        
                        
                            DRelacionado.FacturaEmitida = null;
                            DRelacionado.Pago = null;
                        /*if (pago.Id == null || pago.Id == 0)
                        {*/
                            if (DRelacionado.Traslado != null)
                            {
                                if (DRelacionado.Traslado.Base == 0)
                                {
                                    DRelacionado.Traslado = null;
                                }
                                else
                                {
                                    if (DRelacionado.Traslado.Impuesto == "002" && DRelacionado.Traslado.TasaOCuota == (decimal)0.16 && DRelacionado.Traslado.TipoFactor == c_TipoFactor.Tasa)
                                    {
                                        totalTrasladosBaseIVA16 += DRelacionado.Traslado.Base;
                                        totalTrasladosImpuestoIVA16 += DRelacionado.Traslado.Importe;
                                    }
                                    if (DRelacionado.Traslado.Impuesto == "002" && DRelacionado.Traslado.TasaOCuota == (decimal)0.08 && DRelacionado.Traslado.TipoFactor == c_TipoFactor.Tasa)
                                    {
                                        totalTrasladosBaseIVA8 += DRelacionado.Traslado.Base;
                                        totalTrasladosImpuestoIVA8 += DRelacionado.Traslado.Importe;
                                    }
                                if (DRelacionado.Traslado.Impuesto == "002" && DRelacionado.Traslado.TasaOCuota == (decimal)0.0 && DRelacionado.Traslado.TipoFactor == c_TipoFactor.Tasa)
                                {
                                    totalTrasladosBaseIVA0 += DRelacionado.Traslado.Base;
                                    totalTrasladosImpuestoIVA0 += DRelacionado.Traslado.Importe;
                                }
                                if (DRelacionado.Traslado.Impuesto == "002" && DRelacionado.Traslado.TipoFactor == c_TipoFactor.Exento)
                                {
                                    totalTrasladosBaseIVAExento += DRelacionado.Traslado.Base;
                                }

                            }
                            }
                            else { DRelacionado.Traslado = null; }
                            if (DRelacionado.Retencion != null)
                            {
                                if (DRelacionado.Retencion.Base == 0)
                                {
                                    DRelacionado.Retencion = null;
                                }
                                else
                                {
                                    if (DRelacionado.Retencion.Impuesto == "001") { totalRetencionesISR += DRelacionado.Retencion.Importe; }
                                    if (DRelacionado.Retencion.Impuesto == "002") { totalRetencionesIVA += DRelacionado.Retencion.Importe; }
                                    if (DRelacionado.Retencion.Impuesto == "003") { totalRetencionesIEPS += DRelacionado.Retencion.Importe; }
                                }
                            }
                            else { DRelacionado.Retencion = null; }
                        if (DRelacionado.Id == null || DRelacionado.Id == 0)
                        {
                            _db.DocumentosRelacionados.Add(DRelacionado);
                            _db.SaveChanges();
                        }
                    }
                }
                double montoTPagos = 0;
                if(complementoPago.Pagos != null)
                {
                    foreach(var pg in complementoPago.Pagos)
                    {
                        montoTPagos += pg.Monto;
                    }
                }

                TotalesPagosImpuestos totalPagosImpuesto = new TotalesPagosImpuestos();
                //Totales Pagos Impuestos
                if (complementoPago.TotalesPagoImpuestoId != null)
                {
                    complementoPago.TotalesPagosImpuestos.TotalRetencionesIVA = Decimal.ToDouble(totalRetencionesIVA);
                    complementoPago.TotalesPagosImpuestos.TotalRetencionesISR = Decimal.ToDouble(totalRetencionesISR);
                    complementoPago.TotalesPagosImpuestos.TotalRetencionesIEPS = Decimal.ToDouble(totalRetencionesIEPS);
                    complementoPago.TotalesPagosImpuestos.TotalTrasladosBaseIVA16 = Decimal.ToDouble(totalTrasladosBaseIVA16);
                    complementoPago.TotalesPagosImpuestos.TotalTrasladosBaseIVA8 = Decimal.ToDouble(totalTrasladosBaseIVA8);
                    complementoPago.TotalesPagosImpuestos.TotalTrasladosBaseIVA0 = Decimal.ToDouble(totalTrasladosBaseIVA0);
                    complementoPago.TotalesPagosImpuestos.TotalTrasladosBaseIVAExento = Decimal.ToDouble(totalTrasladosBaseIVAExento);
                    complementoPago.TotalesPagosImpuestos.TotalTrasladosImpuestoIVA16 = Decimal.ToDouble(totalTrasladosImpuestoIVA16);
                    complementoPago.TotalesPagosImpuestos.TotalTrasladosImpuestoIVA8 = Decimal.ToDouble(totalTrasladosImpuestoIVA8);
                    complementoPago.TotalesPagosImpuestos.TotalTrasladosImpuestoIVA0 = Decimal.ToDouble(totalTrasladosImpuestoIVA0);
                    complementoPago.TotalesPagosImpuestos.MontoTotalPagos = montoTPagos;

                    _db.Entry(complementoPago.TotalesPagosImpuestos).State = EntityState.Modified;
                    _db.SaveChanges();

                }
                else
                {
                    
                    totalPagosImpuesto.TotalRetencionesIVA = Decimal.ToDouble(totalRetencionesIVA);
                    totalPagosImpuesto.TotalRetencionesISR = Decimal.ToDouble(totalRetencionesISR);
                    totalPagosImpuesto.TotalRetencionesIEPS = Decimal.ToDouble(totalRetencionesIEPS);
                    totalPagosImpuesto.TotalTrasladosBaseIVA16 = Decimal.ToDouble(totalTrasladosBaseIVA16);
                    totalPagosImpuesto.TotalTrasladosBaseIVA8 = Decimal.ToDouble(totalTrasladosBaseIVA8);
                    totalPagosImpuesto.TotalTrasladosBaseIVA0 = Decimal.ToDouble(totalTrasladosBaseIVA0);
                    totalPagosImpuesto.TotalTrasladosBaseIVAExento = Decimal.ToDouble(totalTrasladosBaseIVAExento);
                    totalPagosImpuesto.TotalTrasladosImpuestoIVA16 = Decimal.ToDouble(totalTrasladosImpuestoIVA16);
                    totalPagosImpuesto.TotalTrasladosImpuestoIVA8 = Decimal.ToDouble(totalTrasladosImpuestoIVA8);
                    totalPagosImpuesto.TotalTrasladosImpuestoIVA0 = Decimal.ToDouble(totalTrasladosImpuestoIVA0);
                    totalPagosImpuesto.MontoTotalPagos = montoTPagos;
                    _db.TotalesPagosImpuestos.Add(totalPagosImpuesto);
                    _db.SaveChanges();
                    //actualiza complemento agregando totales pagos impuestos
                    complementoPago.TotalesPagoImpuestoId = totalPagosImpuesto.Id;
                    _db.Entry(complementoPago).State = EntityState.Modified;
                    _db.SaveChanges();
                }
                
                return RedirectToAction("Index");
            }
            else
            {
                //Identifica los mensaje de error
                var errors = ModelState.Values.Where(E => E.Errors.Count > 0)
                         .SelectMany(E => E.Errors)
                         .Select(E => E.ErrorMessage)
                         .ToList();
                //Identifica el campo del Required
                var modelErrors = ModelState.Where(m => ModelState[m.Key].Errors.Any());
                ModelState.AddModelError("", "Error revisar los campos requeridos");

            }

            return View(complementoPago);
        }

        public ActionResult Generar(int? id)
        {
            
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            ComplementoPago complementoPago = _db.ComplementosPago.Find(id);
            if (complementoPago == null)
            {
                return HttpNotFound();
            }
            PopulaClientes(complementoPago.ReceptorId);
            ViewBag.Controller = "ComplementosPagos";
            ViewBag.Action = "Generar";
            ViewBag.ActionES = "Generar";
            ViewBag.NameHere = "emision";
            return View(complementoPago);
        }

        [HttpPost]
        public ActionResult Generar(ComplementoPago complementoPago)
        {
            ViewBag.Controller = "ComplementosPagos";
            ViewBag.Action = "Generar";
            ViewBag.ActionES = "Generar";
            ViewBag.NameHere = "emision";

            PopulaClientes(complementoPago.ReceptorId);
            string error = "";
            if (ModelState.IsValid)
            {
                try
                {
                    var sucursalId = ObtenerSucursal();

                    //Actualizacion Receptor

                    DateTime fechaDoc = complementoPago.FechaDocumento;
                    var horaHoy = DateTime.Now;
                    var fechaTime = new DateTime(fechaDoc.Year, fechaDoc.Month, fechaDoc.Day, horaHoy.Hour, horaHoy.Minute, horaHoy.Second);
     
                    var complementoPagoDb = _db.ComplementosPago.Find(complementoPago.Id);
                    complementoPagoDb.ReceptorId = complementoPago.ReceptorId;
                    complementoPagoDb.FechaDocumento = fechaTime;
                    _db.Entry(complementoPagoDb).State = EntityState.Modified;
                    _db.SaveChanges();
                   

                    _pagosManager.GenerarComplementoPago(sucursalId, complementoPago.Id, "");
                    return RedirectToAction("Index");
                }
                catch (Exception ex)
                {
                    error = ex.Message;

                }
                if (error == "")
                {
                    return RedirectToAction("Index");
                }
                else
                {
                    ModelState.AddModelError("", error);
                }
            }
            return View(complementoPago);
        }

        public ActionResult Cancelar(int id)
        {
            PopulaMotivoCancelacion();
            ViewBag.Error = null;
            ViewBag.Success = null;
            var complementoP = _db.ComplementosPago.Find(id);
            return PartialView("~/Views/ComplementosPagos/_Cancelacion.cshtml", complementoP);
        }

        [HttpPost]
        public ActionResult Cancelar(ComplementoPago complementoPagos)
        {
            PopulaMotivoCancelacion();
            string error = null;
            var complementoP = _db.ComplementosPago.Find(complementoPagos.Id);
            complementoP.FolioSustitucion = complementoPagos.FolioSustitucion;
            complementoP.MotivoCancelacion = complementoPagos.MotivoCancelacion;
            try
            {
                _pagosManager.Cancelar(complementoP);

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
            return PartialView("~/Views/ComplementosPagos/_Cancelacion.cshtml", complementoP);
        }


        public ActionResult DescargarAcuse(int id)
        {
            var complementoP = _db.ComplementosPago.Find(id);
            string xmlCancelacion = _pagosManager.DowloadAcuseCancelacion(complementoP);
            byte[] byteXml = Encoding.UTF8.GetBytes(xmlCancelacion);
            MemoryStream ms = new MemoryStream(byteXml, 0, 0, true, true);
            string nameArchivo = complementoP.FacturaEmitida.Serie + "-" + complementoP.FacturaEmitida.Folio + "-" + DateTime.Now.ToString("yyyyMMddHHmmssfff");
            Response.AddHeader("content-disposition", "attachment;filename= " + nameArchivo + ".xml");
            Response.Buffer = true;
            Response.Clear();
            Response.OutputStream.Write(ms.GetBuffer(), 0, ms.GetBuffer().Length);
            Response.OutputStream.Flush();
            Response.End();

            return new FileStreamResult(Response.OutputStream, "application/xml");
        }

        public ActionResult DescargarXml(int id)
        {
            var pathCompleto = _pagosManager.GenerarXml(id);
            byte[] archivoFisico = System.IO.File.ReadAllBytes(pathCompleto);
            string contentType = MimeMapping.GetMimeMapping(pathCompleto);

            var cd = new System.Net.Mime.ContentDisposition
            {
                FileName = Path.GetFileName(pathCompleto),
                Inline = false,
            };
            Response.AppendHeader("Content-Disposition", cd.ToString());
            return File(archivoFisico, contentType);
        }
        public ActionResult DescargarPDF(int id)
        {
            ComprobanteCFDI oComprobante = new ComprobanteCFDI();
            ComprobanteCFDI33 oComprobante33 = new ComprobanteCFDI33();
            byte[] archivoFisico =  new byte[255];
            var complementoPago = _db.ComplementosPago.Find(id);
            //checar version del CFDI
            string CadenaXML = System.Text.Encoding.UTF8.GetString(complementoPago.FacturaEmitida.ArchivoFisicoXml);
            string versionCfdi = _decodifica.LeerValorXML(CadenaXML, "Version", "Comprobante");
            if(versionCfdi == "3.3") 
            {
                oComprobante33 = _creationFile.DeserealizarXmlPagos10(id);
                archivoFisico = _creationFile.GeneraPDFPagos33(oComprobante33, id);
            
            } else { 
                oComprobante = _creationFile.DeserealizarXmlPagos20(id);
                archivoFisico = _creationFile.GeneraPDFPagos(oComprobante, id);
                
            }
            
            
            MemoryStream ms = new MemoryStream(archivoFisico, 0, 0, true, true);
            string nameArchivo = complementoPago.FacturaEmitida.Serie + "-" + complementoPago.FacturaEmitida.Folio + "-" + DateTime.Now.ToString("yyyyMMddHHmmssfff");
            Response.AddHeader("content-disposition", "attachment;filename= " + nameArchivo + ".pdf");
            Response.Buffer = true;
            Response.Clear();
            Response.OutputStream.Write(ms.GetBuffer(), 0, ms.GetBuffer().Length);
            Response.OutputStream.Flush();
            Response.End();


            return new FileStreamResult(Response.OutputStream, "application/pdf");
        }
        #endregion

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _db.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Popula Forma

        private int ObtenerGrupo()
        {
            return Convert.ToInt32(Session["GrupoId"]);
        }

        private int ObtenerSucursal()
        {
            return Convert.ToInt32(Session["SucursalId"]);
        }

        private void PopulaBancos(int sucursalId, int? bancoReceptorId = null, int? bancoEmisorId = null)
        {
            var popularDropDowns = new PopularDropDowns(ObtenerGrupo(), true);

            ViewBag.BancoOrdenanteId = popularDropDowns.PopulaBancosClientes(0, bancoReceptorId);
            ViewBag.BancoBeneficiarioId = popularDropDowns.PopulaBancosSucursales(sucursalId, bancoEmisorId);
        }

        private void PopulaClientes(int? receptorId = null)
        {
            var popularDropDowns = new PopularDropDowns(ObtenerSucursal(), true);

            ViewBag.ReceptorId = popularDropDowns.PopulaClientes(receptorId);
        }

        private void PopulaFacturas(int clienteId, int? facturaId = null)
        {
            var popularDropDowns = new PopularDropDowns(ObtenerSucursal(), true);

            ViewBag.FacturaEmitidaId = popularDropDowns.PopulaFacturasEmitidas(true, clienteId, facturaId);
        }

        private void PopulaPagos(int? complementoPagoId = null, int? pagoId = null)
        {
            var popularDropDowns = new PopularDropDowns(ObtenerSucursal(), true);

            ViewBag.PagoId = popularDropDowns.PopulaPagos(complementoPagoId, pagoId);
        }

        private void PopulaCfdiRelacionado(int? cfdiRelacionadoId = null)
        {
            var popularDropDowns = new PopularDropDowns(ObtenerSucursal(), true);

            ViewBag.CfdiRelacionadoId = popularDropDowns.PopulaFacturasEmitidas(false, 0, cfdiRelacionadoId);
        }
        private void PopulaTipoRelacion()
        {
            var popularDropDowns = new PopularDropDowns(ObtenerSucursal(), true);
            ViewBag.tipoRelacion = (popularDropDowns.PopulaTipoRelacion());
        }

        private void PopulaFormaPago()
        {
            var popularDropDowns = new PopularDropDowns(ObtenerSucursal(), true);
            ViewBag.formaPago = (popularDropDowns.PopulaFormaPago());
        }

        private void PopulaObjetoImpuesto()
        {
            var popularDropDowns = new PopularDropDowns(ObtenerSucursal(), true);
            ViewBag.objetoImpuesto = (popularDropDowns.PopulaObjetoImpuesto());
        }

        private void PopulaImpuestoSat()
        {
            var popularDropDowns = new PopularDropDowns(ObtenerSucursal(), true);
            ViewBag.ImpuestoSat = (popularDropDowns.PopulaImpuestoSat());
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

        private void PopulaExportacion()
        {
            var popularDropDowns = new PopularDropDowns(ObtenerSucursal(), true);
            ViewBag.exportacion = (popularDropDowns.PopulaExportacion());
        }

        private void PopulaEstatus()
        {
            List<SelectListItem> items = new List<SelectListItem>();
            items.Add(new SelectListItem { Text = "Generado", Value = "true" });
            items.Add(new SelectListItem { Text = "Pendiente", Value = "false" });
            ViewBag.StatusCP = items;
        }
        #endregion

        #region Operaciones Archivos

        private String SubeArchivo(int indice)
        {
            if (Request.Files.Count > 0)
            {
                var archivo = Request.Files[indice];
                if (archivo.ContentLength > 0)
                {
                    var operacionesStreams = new OperacionesStreams();
                    var nombreArchivo = Path.GetFileName(archivo.FileName);

                    var pathDestino = Path.Combine(Server.MapPath("~/Archivos/CargasMasivas/"), archivo.FileName);
                    Stream fileStream = archivo.InputStream;
                    var mStreamer = new MemoryStream();
                    mStreamer.SetLength(fileStream.Length);
                    fileStream.Read(mStreamer.GetBuffer(), 0, (int)fileStream.Length);
                    mStreamer.Seek(0, SeekOrigin.Begin);
                    operacionesStreams.StreamArchivo(mStreamer, pathDestino);
                    return pathDestino;
                }
            }
            throw new Exception("Favor de cargar por lo menos un archivo");
        }

        #endregion

        public JsonResult GetDataFactura(int FacturaemitidaId)
        {
            var popularDropDowns = new PopularDropDowns(ObtenerSucursal(), true);
            int sucursalId = ObtenerSucursal();
            FacturaEmitida data = _db.FacturasEmitidas.Where(a => a.Id == FacturaemitidaId && a.EmisorId == sucursalId).FirstOrDefault();
            var impuestosDTO =  _decodifica.DecodificarFactura(data);
            return Json(impuestosDTO, JsonRequestBehavior.AllowGet);
        }
    }
}
