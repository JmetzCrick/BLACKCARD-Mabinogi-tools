/* alert */
function del1(){
	alert("�غ����Դϴ�.");
}

$(document).ready(function() {

	// 1 : default(axis x). responsive(using css), callback test
	$("#touchFlow").touchFlow({
		initComplete : function (e) {
			$("#throw_nav_debug").prepend('touchFlow init !!<br>');
		},
		stopped : function (e) {
			$("#throw_nav_debug").prepend('touchFlow stopped. posX : ' + e.posX + ' !!<br>');
		},
		resizeend : function (e) {
			$("#throw_nav_debug").prepend('Window resizeend. listW : ' + this.listw + ' !!<br>');
		}
	});

	// menu type main
	$("#touchFlow0").touchFlow({
		axis : "x",
		page : $("#touchFlow0 li.on").index()
	});

	// menu type1
	$("#touchFlow1").touchFlow({
		axis : "x",
		page : $("#touchFlow2 li.on").index()
	});

	// menu type2
	$("#touchFlow2").touchFlow({
		axis : "x",
		page : $("#touchFlow1 li.on").index()
	});

	// ������ ���� ��� ����Ϸ� ����
	$(".content a").each(function() {

		var this_url = $(this).attr("href");
		this_url = this_url.toLowerCase();
		this_url = this_url.replace("/page/community/", "/m/community/")
		this_url = this_url.replace("/page/news/", "/m/news/")
		this_url = this_url.replace("/page/ucc/", "/m/ucc/")
		// Ȯ������ ������ ����
		if(this_url.indexOf("/itemshop/prob.asp") < 0){
			this_url = this_url.replace("/itemshop/", "/m/itemshop/")
		}

		if(this_url.indexOf("/m/") >= 0) {
			$(this).attr("target", "");
			$(this).attr("href", this_url);
		} else {
			if(this_url.indexOf("javascript:") >= 0) {

			} else {
				$(this).attr("target", "_blank");
			}
		}
	});

	$(".wrap a").each(function() {
		var link_url = $(this).attr("href");
		var link_target = $(this).attr("target");

		if(link_target == "_blank") {
			if(link_url.indexOf("?") > -1) {
				link_url = link_url + "&playtarget=1"
			} else {
				link_url = link_url + "?playtarget=1"
			}
		}

		$(this).attr("href", link_url);
	});

});

/* LNB */
$(document).ready(function(){

	// lnb scroll
	/*var scroll1;
	function loaded() {
		scroll1 = new iScroll('rateScroll1', {click: true, hScroll:true, vScrollbar:true });
	}
	loaded();*/

	/* gnb
	/* mob lnb scroll
	$(function(){

		// lnb scroll
		var scroll1;
		function loaded() {
			scroll1 = new iScroll('rateScroll1', {click: true, hScroll:true, vScrollbar:true });
		}
		loaded();

	});

	$(".gnb li a.dp1").click(function(){
		$(this).parent().addClass("on");
		$(this).parent().parent().find("ul.dp2").css("display", "none");
		$(this).parent().find("ul.dp2").css("display", "block");
	});

	$(".gnb li a.dp2").click(function(){
		$(this).parent().addClass("on");
		$(this).parent().find("ul.dp3").css("display", "block");
	});*/


	/* mainmenu
	$("a.btn_menu_all").click(function(){
		$('html , body').addClass('not_scroll');
		$(".mob_gnb_opa").css("display", "block");
		//$(".gnb").animate({ left: '0' }, 200);
		$(".gnb_wrap").animate({ left: '0' }, 300);
		$(".btn_submenu_close").css("display", "block");
		$(this).data("click", true);
	});
	$("a.btn_mainmenu_close").click(function(){
		$('html , body').removeClass('not_scroll');
		$(".mob_gnb_opa").delay(1000).css("display", "none");
		//$(".gnb").animate({ left: '-292' }, 200);
		$(".gnb_wrap").animate({ left: '-100%' }, 300);
		$(".btn_submenu_close").css("display", "none");
		$(this).data("click", false);
	}); */


	/* submenu
	$(".submenu p.arr_ty01").click(function(){
		if(!$(this).data("click")){
			$(this).addClass("on");
			$(this).parent().find("ul.smenu_dp2").css("display", "block");
			$(this).data("click", true);
		}else{
			$(this).removeClass("on");
			$(this).parent().find("ul.smenu_dp2").css("display", "none");
			$(this).data("click", false);
		}
	});
	$(".submenu li a.dp2").click(function(){
		if(!$(this).data("click")){
			$(this).parent().addClass("arr_up");
			$(this).parent().find("ul.smenu_dp3").css("display", "block");
			$(this).data("click", true);
		}else{
			$(this).parent().removeClass("arr_up");
			$(this).parent().find("ul.smenu_dp3").css("display", "none");
			$(this).data("click", false);
		}
	});
	$(".submenu li a.dp3").click(function(){
		if(!$(this).data("click")){
			$(this).parent().addClass("arr_up");
			$(this).parent().find("ul.smenu_dp4").css("display", "block");
			$(this).data("click", true);
		}else{
			$(this).parent().removeClass("arr_up");
			$(this).parent().find("ul.smenu_dp4").css("display", "none");
			$(this).data("click", false);
		}
	});*/

	/*$("a.btn_menu_r").click(function(){
		$('html , body').addClass('not_scroll');
		$(".mob_gnb_opa").css("display", "block");
		//$(".gnb").animate({ left: '0' }, 400);
		$(".submenu").animate({ right: '0' }, 400);
		$(".btn_submenu_close").css("display", "block");
		$(this).data("click", true);
	});
	$("a.btn_submenu_close").click(function(){
		$('html , body').removeClass('not_scroll');
		$(".mob_gnb_opa").delay(1000).css("display", "none");
		//$(".gnb").animate({ left: '-292' }, 400);
		$(".submenu").animate({ right: '-272' }, 400);
		$(".btn_submenu_close").css("display", "none");
		$(this).data("click", false);
	});*/


	/* pay smenu */
	$(".pay_smenu_tab a.menu01").click(function(){
		$(this).addClass('on');
		$('a.menu02').removeClass('on');
		$(".mail_box_menu01").css("display", "block");
		$(".mail_box_menu02").css("display", "none");
	});
	$(".pay_smenu_tab a.menu02").click(function(){
		$(this).addClass('on');
		$('a.menu01').removeClass('on');
		$(".mail_box_menu01").css("display", "none");
		$(".mail_box_menu02").css("display", "block");
	});


	/* search */
	$(".header .btn_search").click(function(){
		if(!$(this).data("click")){
			$(".input_r").css("display", "block");
			//$(".mob_gnb_opa").css("display", "block");
			$(this).data("click", true);
		}else{
			$(".input_r").css("display", "none");
			//$(".mob_gnb_opa").css("display", "none");
			$(this).data("click", false);
		}
	});
	$(".header .btn_search2").click(function(){
		if(!$(this).data("click")){
			$(".input_r").css("display", "block");
			//$(".mob_gnb_opa").css("display", "block");
			$(this).data("click", true);
		}else{
			$(".input_r").css("display", "none");
			//$(".mob_gnb_opa").css("display", "none");
			$(this).data("click", false);
		}
	});
	$(".header .btn_search_info").click(function(){
		if(!$(this).data("click")){
			$(".search_info_box").css("display", "block");
			//$(".mob_gnb_opa").css("display", "block");
			$(this).data("click", true);
		}else{
			$(".search_info_box").css("display", "none");
			//$(".mob_gnb_opa").css("display", "none");
			$(this).data("click", false);
		}
	});


	/* ���� ��� */
	$(".namecard_list01 p.dp01 a").click(function(){
		if(!$(this).data("click")){
			$(this).addClass('on');
			$(this).parent().parent().find(".inner_box").css("display", "block");
			$(this).data("click", true);
		}else{
			$(this).removeClass('on');
			$(this).parent().parent().find(".inner_box").css("display", "none");
			$(this).data("click", false);
		}
	});


	/* �������
	$(".info_list01 li a.dp1").click(function(){
		if(!$(this).data("click")){
			$(this).addClass("arr_up");
			$(this).parent().find("ul.smenu_dp2").css("display", "block");
			$(this).data("click", true);
		}else{
			$(this).removeClass("arr_up");
			$(this).parent().find("ul.smenu_dp2").css("display", "none");
			$(this).data("click", false);
		}
	});
	$(".info_list01 li p.dp2").click(function(){
		if(!$(this).data("click")){
			$(this).parent().addClass("arr_up");
			$(this).parent().find("ul.smenu_dp3").css("display", "block");
			$(this).data("click", true);
		}else{
			$(this).parent().removeClass("arr_up");
			$(this).parent().find("ul.smenu_dp3").css("display", "none");
			$(this).data("click", false);
		}
	});
	$(".info_list01 li p.dp3").click(function(){
		if(!$(this).data("click")){
			$(this).parent().addClass("arr_up");
			$(this).parent().find("ul.smenu_dp4").css("display", "block");
			$(this).data("click", true);
		}else{
			$(this).parent().removeClass("arr_up");
			$(this).parent().find("ul.smenu_dp4").css("display", "none");
			$(this).data("click", false);
		}
	});

	$(".info_list02 li .list_box").click(function(){
		if(!$(this).data("click")){
			$(this).addClass("on");
			$(this).parent().find("ul.dp2_inbox").css("display", "block");
			$(this).data("click", true);
		}else{
			$(this).removeClass("on");
			$(this).parent().find("ul.dp2_inbox").css("display", "none");
			$(this).data("click", false);
		}
	}); */


	/* ����÷�� */
	$(".view_ty01 .file a.btn_file").click(function(){
		if(!$(this).data("click")){
			$(this).addClass('on');
			$(".file_list01").css("display", "block");
			$(this).data("click", true);
		}else{
			$(this).removeClass('on');
			$(".file_list01").css("display", "none");
			$(this).data("click", false);
		}
	});

	/* ������� */
	$(".view_ty01 .send a.btn_send").click(function(){
		if(!$(this).data("click")){
			$(this).addClass('on');
			$(".send_list01").css("display", "block");
			$(this).data("click", true);
		}else{
			$(this).removeClass('on');
			$(".send_list01").css("display", "none");
			$(this).data("click", false);
		}
	});


	/* select */
	$(".footer .family_wrap p").click(function(){
		if(!$(this).data("click")){
			$(".footer .family_wrap p").addClass('on');
			$(".footer .family_wrap ul").css("display", "block");
			$(this).data("click", true);
		}else{
			$(".footer .family_wrap p").removeClass('on');
			$(".footer .family_wrap ul").css("display", "none");
			$(this).data("click", false);
		}
	});


	/* �޷� ���� */
	$(".cal_month .cal ul li").click(function(){
		$(this).parent().parent().find("li").removeClass('focus');
		$(this).addClass('focus');
	});

	/* ���� */
	$(".cal_month02 p.btn_allday a").click(function(){
		if(!$(this).data("click")){
			$("span.btm_arr").addClass('on');
			$(".more").css("display", "block");
			$("p.add_num").css("display", "none");
			$(this).data("click", true);
		}else{
			$("span.btm_arr").removeClass('on');
			$(".more").css("display", "none");
			$("p.add_num").css("display", "block");
			$(this).data("click", false);
		}
	});

	/* ���� */
	$(".setting_box a.btn_of").click(function(){
		if(!$(this).data("click")){
			$(this).removeClass('on');
			$(this).addClass('off');
			$(this).data("click", true);
		}else{
			$(this).removeClass('off');
			$(this).addClass('on');
			$(this).data("click", false);
		}
	});

});


function popOpen(layerName){
	document.getElementById(layerName).style.display = 'block';
	return false;
}
function popClose(layerName){
	document.getElementById(layerName).style.display = 'none';
	return false;
}

function openUserInfoM(infolayer_id, infolayer_server, infolayer_character) {
	location.href="/m/common/userInfoData.asp?infolayer_id="+ infolayer_id +"&infolayer_server="+ infolayer_server +"&infolayer_character="+ escape(infolayer_character)
}

