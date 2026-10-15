(function () {
    'use strict';

    var tipo = document.getElementById('pagoTipo');
    var campoReferencia = document.getElementById('campoReferencia');
    var referencia = document.getElementById('pagoReferencia');
    var cuota = document.getElementById('pagoCuota');
    var monto = document.getElementById('pagoMonto');
    var ayuda = document.getElementById('ayudaMonto');
    var boton = document.getElementById('usarPendiente');

    // Referencia bancaria
    function actualizarReferencia() {
        if (!tipo || !campoReferencia || !referencia) return;
        var opcion = tipo.options[tipo.selectedIndex];
        var elegido = opcion && opcion.value !== '';
        var efectivo = elegido && opcion.getAttribute('data-efectivo') === '1';
        campoReferencia.classList.toggle('d-none', efectivo);
        referencia.required = elegido && !efectivo;
        if (efectivo) referencia.value = '';
    }

    if (tipo) {
        tipo.addEventListener('change', actualizarReferencia);
        actualizarReferencia();
    }

    //Monto
    function pendiente() {
        if (!cuota) return '';
        var opcion = cuota.options[cuota.selectedIndex];
        return opcion ? (opcion.getAttribute('data-pendiente') || '') : '';
    }

    function textoPendiente() {
        var valor = parseFloat(pendiente());
        if (isNaN(valor)) return 'Puede pagar la cuota completa o un abono parcial.';
        return 'Pendiente de esta cuota: Q ' + valor.toLocaleString('en-US',
            { minimumFractionDigits: 2, maximumFractionDigits: 2 }) + '. Puede pagar la cuota completa o un abono parcial.';
    }

    var montoManual = monto ? monto.value !== '' : false;
    var ultimoSugerido = monto ? monto.value : '';

    if (monto) {
        monto.addEventListener('input', function () {
            montoManual = monto.value !== '' && monto.value !== ultimoSugerido;
        });
    }

    if (cuota && monto) {
        if (ayuda) ayuda.textContent = textoPendiente();

        cuota.addEventListener('change', function () {
            if (ayuda) ayuda.textContent = textoPendiente();
            if (!montoManual) {
                ultimoSugerido = pendiente();
                monto.value = ultimoSugerido;
            }
        });
    }

    if (boton && monto) {
        boton.addEventListener('click', function () {
            ultimoSugerido = pendiente();
            monto.value = ultimoSugerido;
            montoManual = false;
            monto.focus();
        });
    }

    // Un campo inválido cancela el envío
    var formulario = document.getElementById('formPago');
    if (formulario) {
        formulario.addEventListener('invalid', function () {
            formulario.removeAttribute('data-enviado');
        }, true);
    }
})();
