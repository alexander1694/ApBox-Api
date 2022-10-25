﻿using API.Enums;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace API.Operaciones.Facturacion
{
    [Table("facturas_emitidas_temporal")]
    public class FacturaEmitidaTemporal
    {
        [Key]
        public int Id { get; set; }

        public int EmisorId { get; set; }

        public int ReceptorId { get; set; }

        public DateTime Fecha { get; set; }

        public String Serie { get; set; }

        public String Folio { get; set; }

        public String Uuid { get; set; }

        public Double Total { get; set; }

        public c_MetodoPago MetodoPago { get; set; }

        public c_Moneda Moneda { get; set; }
    }
}
